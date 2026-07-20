// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a value produced by <c>Guid.NewGuid()</c> that is used as a secret (SES1004). A GUID is a
/// uniqueness token, not an unguessable one: it carries at most 122 bits of entropy, its structure is
/// public, and it is routinely logged and put in URLs -- fatal properties for a secret. The rule is a
/// name-heuristic: it reports only when the GUID's value flows directly (optionally through
/// <c>.ToString(...)</c>) into a local, field, property, parameter, or return whose identifier matches
/// a curated, high-precision secret vocabulary -- <c>token</c>, <c>secret</c>, <c>password</c>/<c>passwd</c>/
/// <c>pwd</c>, <c>nonce</c>, <c>salt</c>, <c>otp</c>, an API key, a session id/key, a verification code, or a
/// reset token -- matched on word boundaries so an ordinary <c>Guid.NewGuid()</c> used as an id is never
/// touched. The suggestion is <c>System.Security.Cryptography.RandomNumberGenerator</c>; the rule resolves
/// that type once per compilation and registers nothing when it is absent, so a project that cannot act on
/// the diagnostic never receives it. There is no code fix because the correct replacement call
/// (<c>GetBytes</c>, <c>GetInt32</c>, <c>GetHexString</c>, <c>GetString</c>, and its size) depends on the shape
/// of the secret being minted.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1004GuidAsSecretAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The <c>System.Guid.NewGuid</c> factory method name inspected syntactically before binding.</summary>
    private const string NewGuidMethodName = "NewGuid";

    /// <summary>The <c>ToString</c> method name skipped when it wraps the GUID before it reaches a target.</summary>
    private const string ToStringMethodName = "ToString";

    /// <summary>The metadata name of the GUID type whose factory is matched.</summary>
    private const string GuidMetadataName = "System.Guid";

    /// <summary>The metadata name of the cryptographic RNG the rule suggests; the gate for the whole rule.</summary>
    private const string RandomNumberGeneratorMetadataName = "System.Security.Cryptography.RandomNumberGenerator";

    /// <summary>Single-concept secret words matched against a whole identifier word (case-insensitive).</summary>
    private static readonly string[] SecretWords =
    [
        "token",
        "secret",
        "password",
        "passwd",
        "pwd",
        "nonce",
        "salt",
        "otp",
        "apikey",
        "sessionid",
        "sessionkey",
        "verificationcode",
        "resettoken"
    ];

    /// <summary>Multi-word secret terms matched as a consecutive run of identifier words (e.g. <c>apiKey</c>).</summary>
    private static readonly string[][] SecretWordRuns =
    [
        ["api", "key"],
        ["session", "id"],
        ["session", "key"],
        ["verification", "code"],
        ["reset", "token"]
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.GuidAsSecret);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Gate the whole rule on the API we suggest: if a project cannot call RandomNumberGenerator, the
            // diagnostic would not be actionable, so register nothing. The GUID type is resolved for the match
            // and passed through; when it is absent (impossible once the RNG resolved) the symbol comparison
            // simply never matches, so no separate guard is needed.
            if (start.Compilation.GetTypeByMetadataName(RandomNumberGeneratorMetadataName) is null)
            {
                return;
            }

            var guidType = start.Compilation.GetTypeByMetadataName(GuidMetadataName);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, guidType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1004 for a <c>Guid.NewGuid()</c> call whose value flows into a secret-named target.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="guidType">The resolved <c>System.Guid</c> type used to confirm the factory call; never matches when absent.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol? guidType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a parameterless '.NewGuid()' member call. Anything else is cheaply rejected.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: NewGuidMethodName }
            || invocation.ArgumentList.Arguments.Count != 0)
        {
            return;
        }

        // A GUID is often stringified before it is stored; treat 'Guid.NewGuid().ToString(...)' as the value.
        var effective = SkipToStringCalls(invocation);

        // Resolve the secret-named target syntactically where possible (only the argument shape binds), so a
        // non-secret target skips the semantic Guid bind below entirely.
        if (TryGetSecretTargetName(effective, context.SemanticModel, context.CancellationToken) is not { } targetName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: NewGuidMethodName, IsStatic: true } method
            || !method.Parameters.IsEmpty
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, guidType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.GuidAsSecret,
            invocation.SyntaxTree,
            invocation.Span,
            targetName));
    }

    /// <summary>Walks past any <c>.ToString(...)</c> calls wrapping the GUID to the outermost value expression.</summary>
    /// <param name="invocation">The <c>Guid.NewGuid()</c> invocation.</param>
    /// <returns>The outermost expression whose value is the GUID.</returns>
    private static ExpressionSyntax SkipToStringCalls(ExpressionSyntax invocation)
    {
        var current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: ToStringMethodName } access
            && access.Expression == current
            && access.Parent is InvocationExpressionSyntax outer
            && outer.Expression == access)
        {
            current = outer;
        }

        return current;
    }

    /// <summary>Returns the secret-named target the GUID value flows into, or <see langword="null"/>.</summary>
    /// <param name="value">The GUID value expression (already unwrapped of <c>.ToString(...)</c>).</param>
    /// <param name="model">The semantic model, used only to bind the argument-to-parameter shape.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching secret target name, or <see langword="null"/> when the target is not secret-named.</returns>
    private static string? TryGetSecretTargetName(ExpressionSyntax value, SemanticModel model, CancellationToken cancellationToken)
    {
        // A value expression has exactly one parent slot, so each arm identifies the sink unambiguously.
        var target = value.Parent switch
        {
            // The initializer of a local or field declaration: the declared variable is the sink.
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.ValueText,

            // The initializer of an auto-property: the property is the sink.
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,

            // The right-hand side of a simple assignment: the left-hand target is the sink.
            AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment => GetAssignmentTargetName(assignment.Left),

            // A return statement: the enclosing member is the sink.
            ReturnStatementSyntax returnStatement => GetEnclosingMemberName(returnStatement),

            // An expression-bodied member: the enclosing member is the sink.
            ArrowExpressionClauseSyntax arrow => GetEnclosingMemberName(arrow),

            // An argument: the parameter it binds to is the sink.
            ArgumentSyntax argument => GetArgumentParameterName(argument, model, cancellationToken),

            _ => null,
        };

        return Match(target);
    }

    /// <summary>Returns the name of the parameter an argument binds to, or <see langword="null"/>.</summary>
    /// <param name="argument">The argument carrying the GUID value.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The bound parameter's name, or <see langword="null"/> when the argument does not bind to one.</returns>
    private static string? GetArgumentParameterName(ArgumentSyntax argument, SemanticModel model, CancellationToken cancellationToken)
        => model.GetOperation(argument, cancellationToken) is IArgumentOperation { Parameter.Name: { } parameterName } ? parameterName : null;

    /// <summary>Returns the simple name written on the left-hand side of an assignment, or <see langword="null"/>.</summary>
    /// <param name="left">The assignment target expression.</param>
    /// <returns>The identifier or member name assigned to, or <see langword="null"/> for a computed target.</returns>
    private static string? GetAssignmentTargetName(ExpressionSyntax left)
        => left switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the name of the member (method, property, accessor's property, or local function) enclosing a node.</summary>
    /// <param name="node">The node whose enclosing member name is wanted.</param>
    /// <returns>The enclosing member's name, or <see langword="null"/> when none applies.</returns>
    private static string? GetEnclosingMemberName(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText;
                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.Identifier.ValueText;

                // A getter block's return also lands here: the walk passes its accessor to reach the property.
                case PropertyDeclarationSyntax property:
                    return property.Identifier.ValueText;

                // Stop at a lambda so a value it returns is never attributed to the enclosing member's name.
                case AnonymousFunctionExpressionSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Returns the supplied name when it matches the curated secret vocabulary, else <see langword="null"/>.</summary>
    /// <param name="name">The candidate target name (may be <see langword="null"/>).</param>
    /// <returns>The name when it is secret-shaped, otherwise <see langword="null"/>.</returns>
    private static string? Match(string? name)
        => name is not null && IsSecretName(name) ? name : null;

    /// <summary>Returns whether an identifier reads as a secret under the curated word-boundary heuristic.</summary>
    /// <param name="name">The identifier to test.</param>
    /// <returns><see langword="true"/> when a whole word equals a secret term or a consecutive word run matches.</returns>
    private static bool IsSecretName(string name)
    {
        // A valid identifier always yields at least one word; an empty list simply matches nothing below.
        var words = SplitIntoWords(name);

        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            for (var t = 0; t < SecretWords.Length; t++)
            {
                if (string.Equals(word, SecretWords[t], StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        for (var r = 0; r < SecretWordRuns.Length; r++)
        {
            if (ContainsWordRun(words, SecretWordRuns[r]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a list of words contains a run of words in order (e.g. <c>api</c>, <c>key</c>).</summary>
    /// <param name="words">The identifier's words.</param>
    /// <param name="run">The consecutive words to find.</param>
    /// <returns><see langword="true"/> when the run appears in order.</returns>
    private static bool ContainsWordRun(List<string> words, string[] run)
    {
        for (var start = 0; start + run.Length <= words.Count; start++)
        {
            var matched = true;
            for (var offset = 0; offset < run.Length; offset++)
            {
                if (!string.Equals(words[start + offset], run[offset], StringComparison.Ordinal))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Splits an identifier into lowercase words on separators, case transitions, and acronym ends.</summary>
    /// <param name="name">The identifier to split.</param>
    /// <returns>The lowercase words; empty when the identifier holds no letters or digits.</returns>
    private static List<string> SplitIntoWords(string name)
    {
        var words = new List<string>(4);
        var start = -1;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsLetterOrDigit(c))
            {
                FlushWord(words, name, start, i);
                start = -1;
                continue;
            }

            if (start < 0)
            {
                start = i;
                continue;
            }

            // aB -> a|B, and a1 / 1a digit boundaries.
            if (IsWordBoundary(name, i))
            {
                FlushWord(words, name, start, i);
                start = i;
            }
        }

        FlushWord(words, name, start, name.Length);
        return words;
    }

    /// <summary>Returns whether a boundary falls immediately before the character at <paramref name="i"/>.</summary>
    /// <param name="name">The identifier being split.</param>
    /// <param name="i">The index of the current character (always &gt; 0 and letter-or-digit).</param>
    /// <returns><see langword="true"/> when a new word starts at <paramref name="i"/>.</returns>
    private static bool IsWordBoundary(string name, int i)
    {
        var previous = name[i - 1];
        var current = name[i];

        // lower/digit followed by an upper letter: 'tokenValue' -> token|Value.
        if (char.IsUpper(current) && !char.IsUpper(previous))
        {
            return true;
        }

        // end of an acronym run: 'APIKey' -> API|Key (upper preceded by upper, followed by lower).
        if (char.IsUpper(current) && char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]))
        {
            return true;
        }

        // letter/digit transition either way: 'otp2' / '2fa'.
        return char.IsDigit(current) != char.IsDigit(previous);
    }

    /// <summary>Appends the lowercased span <c>[start, end)</c> of <paramref name="name"/> as a word when non-empty.</summary>
    /// <param name="words">The accumulating word list.</param>
    /// <param name="name">The identifier being split.</param>
    /// <param name="start">The inclusive word start, or a negative value when no word is open.</param>
    /// <param name="end">The exclusive word end.</param>
    private static void FlushWord(List<string> words, string name, int start, int end)
    {
        // Callers only pass end > start once a word is open (start >= 0), so a single guard suffices.
        if (start < 0)
        {
            return;
        }

        words.Add(name.Substring(start, end - start).ToLowerInvariant());
    }
}
