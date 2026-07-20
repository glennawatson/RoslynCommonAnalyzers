// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Replaces a byte-buffer <c>SequenceEqual</c> comparison with
/// <c>CryptographicOperations.FixedTimeEquals(a, b)</c> (SES1005). Only the plain content-comparison
/// shape is fixed -- both operands must be <c>byte[]</c> or a byte span, there must be no
/// <c>IEqualityComparer</c> overload in play, and <c>FixedTimeEquals</c> must resolve in the
/// compilation. The <c>==</c>/<c>!=</c>, <c>.Equals</c>, and <c>string</c> shapes carry different
/// semantics (reference equality, or a value type <c>FixedTimeEquals</c> cannot accept) and are
/// reported without a fix. When <c>System.Security.Cryptography</c> is not imported the type is
/// spelled fully qualified so the fix never breaks the build.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Ses1005NonConstantTimeSecretComparisonCodeFixProvider))]
[Shared]
public sealed class Ses1005NonConstantTimeSecretComparisonCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The metadata name of the type hosting the constant-time comparison method.</summary>
    private const string CryptographicOperationsMetadataName = "System.Security.Cryptography.CryptographicOperations";

    /// <summary>The namespace probed for in the file's usings.</summary>
    private const string CryptographicOperationsNamespace = "System.Security.Cryptography";

    /// <summary>The unqualified name of the type hosting the constant-time comparison method.</summary>
    private const string CryptographicOperationsTypeName = "CryptographicOperations";

    /// <summary>The name of the constant-time comparison method.</summary>
    private const string FixedTimeEqualsMethodName = "FixedTimeEquals";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(SecurityRules.NonConstantTimeSecretComparison.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Compare in constant time with CryptographicOperations.FixedTimeEquals",
            nameof(Ses1005NonConstantTimeSecretComparisonCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported byte-buffer <c>SequenceEqual</c> and builds its <c>FixedTimeEquals</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape is not fixable.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || !Ses1005NonConstantTimeSecretComparisonAnalyzer.TryGetFixableByteComparison(model, invocation, CancellationToken.None, out var left, out var right)
            || !HasFixedTimeEquals(model.Compilation))
        {
            return null;
        }

        var replacement = BuildFixedTimeEquals(root, left, right).WithTriviaFrom(invocation);
        return new NodeReplacement(invocation, replacement);
    }

    /// <summary>Returns whether the compilation exposes a static <c>FixedTimeEquals</c> to bind the fix to.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when a static <c>FixedTimeEquals</c> is present.</returns>
    private static bool HasFixedTimeEquals(Compilation compilation)
    {
        foreach (var member in ResolveCandidateMembers(compilation))
        {
            if (member is IMethodSymbol { IsStatic: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the <c>FixedTimeEquals</c> members of the crypto type, or an empty span-safe list when it is absent.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns>The matching members; empty when the type does not resolve.</returns>
    private static ImmutableArray<ISymbol> ResolveCandidateMembers(Compilation compilation)
        => compilation.GetTypeByMetadataName(CryptographicOperationsMetadataName) is { } type
            ? type.GetMembers(FixedTimeEqualsMethodName)
            : ImmutableArrays.Of<ISymbol>();

    /// <summary>Builds <c>CryptographicOperations.FixedTimeEquals(left, right)</c>, qualifying the type when it is not imported.</summary>
    /// <param name="root">The syntax root, probed for the crypto namespace import.</param>
    /// <param name="left">The first operand.</param>
    /// <param name="right">The second operand.</param>
    /// <returns>The replacement invocation.</returns>
    private static InvocationExpressionSyntax BuildFixedTimeEquals(SyntaxNode root, ExpressionSyntax left, ExpressionSyntax right)
    {
        var target = HasCryptographicOperationsImport(root)
            ? (ExpressionSyntax)SyntaxFactory.IdentifierName(CryptographicOperationsTypeName)
            : SyntaxFactory.ParseExpression($"global::{CryptographicOperationsNamespace}.{CryptographicOperationsTypeName}");

        var access = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            target,
            SyntaxFactory.IdentifierName(FixedTimeEqualsMethodName));

        var arguments = SyntaxFactory.SeparatedList<ArgumentSyntax>(
        [
            SyntaxFactory.Argument(left.WithoutTrivia()),
            SyntaxFactory.Argument(right.WithoutTrivia()).WithLeadingTrivia(SyntaxFactory.Space),
        ]);

        return SyntaxFactory.InvocationExpression(access, SyntaxFactory.ArgumentList(arguments));
    }

    /// <summary>Returns whether the compilation unit imports <c>System.Security.Cryptography</c>.</summary>
    /// <param name="root">The syntax root.</param>
    /// <returns><see langword="true"/> when a matching using directive exists.</returns>
    private static bool HasCryptographicOperationsImport(SyntaxNode root)
    {
        foreach (var directive in (root as CompilationUnitSyntax)?.Usings ?? default)
        {
            if (directive.Alias is null
                && !directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                && directive.StaticKeyword.IsKind(SyntaxKind.None)
                && directive.Name?.ToString() == CryptographicOperationsNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
