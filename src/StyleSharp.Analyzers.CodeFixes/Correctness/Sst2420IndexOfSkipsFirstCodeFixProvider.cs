// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Corrects an index-of comparison that skips the first position (SST2420). Where a matching
/// <c>Contains</c> overload exists it becomes a membership test; otherwise — on frameworks that lack the
/// overload — it degrades to <c>&gt;= 0</c>, which is correct everywhere.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2420IndexOfSkipsFirstCodeFixProvider))]
[Shared]
public sealed class Sst2420IndexOfSkipsFirstCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.IndexOfSkipsFirst.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Test membership without skipping the first position",
            nameof(Sst2420IndexOfSkipsFirstCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comparison and rewrites it to a correct membership test.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BinaryExpressionSyntax>() is not { } comparison
            || GetIndexOfCall(comparison) is not { Expression: MemberAccessExpressionSyntax member } invocation)
        {
            return null;
        }

        ExpressionSyntax replacement = member.Name.Identifier.ValueText == "IndexOf" && TryBuildContains(model, comparison, invocation, member) is { } contains
            ? contains
            : Relax(comparison);
        return new NodeReplacement(comparison, replacement.WithTriviaFrom(comparison));
    }

    /// <summary>Builds a <c>Contains</c> call and returns it only when it binds to a boolean method.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="comparison">The reported comparison.</param>
    /// <param name="invocation">The index-of invocation.</param>
    /// <param name="member">The index-of member access.</param>
    /// <returns>The bound <c>Contains</c> call, or <see langword="null"/> when it does not resolve.</returns>
    private static InvocationExpressionSyntax? TryBuildContains(
        SemanticModel model,
        BinaryExpressionSyntax comparison,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member)
    {
        var contains = invocation.WithExpression(member.WithName(SyntaxFactory.IdentifierName("Contains")));
        return model.GetSpeculativeSymbolInfo(comparison.SpanStart, contains, SpeculativeBindingOption.BindAsExpression).Symbol
            is IMethodSymbol { ReturnType.SpecialType: SpecialType.System_Boolean }
            ? contains
            : null;
    }

    /// <summary>Relaxes the strict comparison to one that includes the first position.</summary>
    /// <param name="comparison">The reported comparison.</param>
    /// <returns><c>&gt;= 0</c> or <c>0 &lt;=</c>.</returns>
    private static BinaryExpressionSyntax Relax(BinaryExpressionSyntax comparison)
    {
        var isGreater = comparison.IsKind(SyntaxKind.GreaterThanExpression);
        var kind = isGreater ? SyntaxKind.GreaterThanOrEqualExpression : SyntaxKind.LessThanOrEqualExpression;
        var tokenKind = isGreater ? SyntaxKind.GreaterThanEqualsToken : SyntaxKind.LessThanEqualsToken;
        var operatorToken = SyntaxFactory.Token(comparison.OperatorToken.LeadingTrivia, tokenKind, comparison.OperatorToken.TrailingTrivia);
        return SyntaxFactory.BinaryExpression(kind, comparison.Left, operatorToken, comparison.Right);
    }

    /// <summary>Gets the index-of call of a <c>&gt; 0</c> / <c>0 &lt;</c> comparison.</summary>
    /// <param name="comparison">The relational comparison.</param>
    /// <returns>The invocation, or <see langword="null"/>.</returns>
    private static InvocationExpressionSyntax? GetIndexOfCall(BinaryExpressionSyntax comparison)
    {
        var invocationSide = comparison.IsKind(SyntaxKind.GreaterThanExpression) ? comparison.Left : comparison.Right;
        var zeroSide = comparison.IsKind(SyntaxKind.GreaterThanExpression) ? comparison.Right : comparison.Left;
        return zeroSide is LiteralExpressionSyntax { Token.Value: int and 0 }
            && invocationSide is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation
            && member.Name.Identifier.ValueText is "IndexOf" or "LastIndexOf"
                ? invocation
                : null;
    }
}
