// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a password-like value hashed with a fast, general-purpose hash instead of a slow password
/// key-derivation function (SES1009). The rule reports a static one-shot <c>HashData</c> or an instance
/// <c>ComputeHash</c> on <c>MD5</c>, <c>SHA1</c>, <c>SHA256</c>, <c>SHA384</c>, or <c>SHA512</c> when the
/// hashed input reads as a password: the argument's identifier or member name -- or, when the argument is
/// a <c>System.Text.Encoding.GetBytes(...)</c> call, the encoded value's name -- contains (case-insensitive)
/// <c>password</c>, <c>passwd</c>, <c>pwd</c>, <c>passphrase</c>, or <c>credential</c>. A fast hash is cheap
/// to brute-force even when salted, so passwords need a deliberately slow KDF (<c>Rfc2898DeriveBytes</c>/
/// <c>Pbkdf2</c>, Argon2, and the like); this is orthogonal to the iteration-count check, which only governs a
/// KDF's work factor. Detection is a high-precision name-and-API heuristic: hashing arbitrary, non-password
/// data is never reported. The rule resolves the fast-hash types once per compilation and registers nothing
/// when none are present, so a target framework without them pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1009FastPasswordHashAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the static one-shot hashing method, declared on each fast-hash type.</summary>
    private const string HashDataMethodName = "HashData";

    /// <summary>The name of the instance hashing method, inherited from <c>HashAlgorithm</c>.</summary>
    private const string ComputeHashMethodName = "ComputeHash";

    /// <summary>The name of the encoding method whose argument carries the hashed value's name.</summary>
    private const string GetBytesMethodName = "GetBytes";

    /// <summary>The metadata names of the fast, general-purpose hash types the rule gates on and matches.</summary>
    private static readonly string[] FastHashMetadataNames =
    [
        "System.Security.Cryptography.MD5",
        "System.Security.Cryptography.SHA1",
        "System.Security.Cryptography.SHA256",
        "System.Security.Cryptography.SHA384",
        "System.Security.Cryptography.SHA512",
    ];

    /// <summary>The curated, high-precision fragments that mark a hashed input as a password.</summary>
    private static readonly string[] PasswordNameFragments =
    [
        "password",
        "passwd",
        "pwd",
        "passphrase",
        "credential",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.FastPasswordHash);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Gate the whole rule on the fast-hash types resolving: on a framework without them nothing is
            // registered and the clean path costs nothing.
            var fastHashTypes = ResolveFastHashTypes(start.Compilation);
            if (fastHashTypes.Length == 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, fastHashTypes), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1009 for a fast-hash <c>HashData</c>/<c>ComputeHash</c> call over a password-named input.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="fastHashTypes">The gated fast-hash types resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol[] fastHashTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.HashData(...)'/'.ComputeHash(...)' call carrying at least one argument.
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        var methodName = member.Name.Identifier.ValueText;
        var isHashData = methodName == HashDataMethodName;
        if (!isHashData && methodName != ComputeHashMethodName)
        {
            return;
        }

        // Name heuristic: the hashed input must read as a password before anything is bound.
        if (GetPasswordName(invocation.ArgumentList.Arguments[0].Expression) is not { } passwordName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        // 'HashData' is a static one-shot declared on each fast-hash type, so its containing type identifies the
        // algorithm. 'ComputeHash' is inherited from the abstract 'HashAlgorithm' base, so its algorithm comes
        // from the receiver's static type instead.
        var hashType = isHashData
            ? method.ContainingType
            : context.SemanticModel.GetTypeInfo(member.Expression, context.CancellationToken).Type;

        if (!IsFastHashType(hashType, fastHashTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.FastPasswordHash,
            invocation.SyntaxTree,
            invocation.Span,
            passwordName));
    }

    /// <summary>Resolves the fast-hash types present in the compilation, in declared order.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns>The resolved fast-hash types; empty when none are present.</returns>
    private static INamedTypeSymbol[] ResolveFastHashTypes(Compilation compilation)
    {
        var resolved = new List<INamedTypeSymbol>(FastHashMetadataNames.Length);
        for (var i = 0; i < FastHashMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(FastHashMetadataNames[i]) is { } type)
            {
                resolved.Add(type);
            }
        }

        return [.. resolved];
    }

    /// <summary>Returns the password-signalling name of a hashed input, or <see langword="null"/>.</summary>
    /// <param name="input">The first argument passed to the hashing method.</param>
    /// <returns>The matching password name, or <see langword="null"/> when the input does not read as a password.</returns>
    private static string? GetPasswordName(ExpressionSyntax input)
    {
        // 'Encoding.GetBytes(password)' carries the password name on the encoded value, not the GetBytes call.
        var candidate = UnwrapEncodingGetBytes(input);
        var name = GetSymbolName(candidate);
        return name is not null && ContainsPasswordFragment(name) ? name : null;
    }

    /// <summary>Unwraps a <c>*.GetBytes(x)</c> call to its first argument so the encoded value's name is inspected.</summary>
    /// <param name="input">The hashed-input expression.</param>
    /// <returns>The encoded value expression when the input is a <c>GetBytes(...)</c> call; otherwise the input.</returns>
    private static ExpressionSyntax UnwrapEncodingGetBytes(ExpressionSyntax input)
        => input is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetBytesMethodName } } getBytes
            && getBytes.ArgumentList.Arguments.Count >= 1
            ? getBytes.ArgumentList.Arguments[0].Expression
            : input;

    /// <summary>Extracts the rightmost meaningful identifier from a hashed-input expression.</summary>
    /// <param name="expression">The input expression.</param>
    /// <returns>The input name, or <see langword="null"/> when none can be identified.</returns>
    private static string? GetSymbolName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        InvocationExpressionSyntax invocation => GetSymbolName(invocation.Expression),
        _ => null,
    };

    /// <summary>Returns whether a name contains any password fragment, case-insensitively and without allocating.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name contains a password fragment.</returns>
    private static bool ContainsPasswordFragment(string name)
    {
        for (var i = 0; i < PasswordNameFragments.Length; i++)
        {
            if (name.IndexOf(PasswordNameFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is exactly one of the gated fast-hash types.</summary>
    /// <param name="type">The candidate algorithm type.</param>
    /// <param name="fastHashTypes">The gated fast-hash types.</param>
    /// <returns><see langword="true"/> when the type is a fast, general-purpose hash.</returns>
    private static bool IsFastHashType(ITypeSymbol? type, INamedTypeSymbol[] fastHashTypes)
    {
        for (var i = 0; i < fastHashTypes.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(type, fastHashTypes[i]))
            {
                return true;
            }
        }

        return false;
    }
}
