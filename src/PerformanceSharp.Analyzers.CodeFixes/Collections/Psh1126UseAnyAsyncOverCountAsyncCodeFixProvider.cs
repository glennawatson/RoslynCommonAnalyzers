// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported emptiness comparison of an awaited <c>CountAsync()</c> result to the
/// short-circuiting <c>AnyAsync()</c> form (PSH1126): <c>await q.CountAsync() &gt; 0</c> becomes
/// <c>await q.AnyAsync()</c> and <c>await q.CountAsync() == 0</c> becomes
/// <c>!await q.AnyAsync()</c>, carrying any arguments over unchanged. The rewritten call is
/// speculatively bound and required to resolve to an <c>AnyAsync</c> returning a boolean
/// awaitable before the fix is offered, so a provider without a matching sibling never gets a
/// replacement that would not compile.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider))]
[Shared]
public sealed class Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseAnyAsyncOverCountAsync.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use AnyAsync()", nameof(Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported comparison with its AnyAsync() form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="comparison">The comparison expression to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel model, BinaryExpressionSyntax comparison)
        => TryGetReplacement(model, comparison, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement!))
            : document;

    /// <summary>Resolves the reported comparison and builds its AnyAsync() replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is BinaryExpressionSyntax binary
            && TryGetReplacement(model, binary, out var replacement)
            ? new NodeReplacement(binary, replacement!)
            : null;

    /// <summary>Builds the AnyAsync() replacement for a reported comparison, and proves it binds.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="binary">The comparison expression to rewrite.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized and binds.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(SemanticModel model, BinaryExpressionSyntax binary, out ExpressionSyntax? replacement)
    {
        replacement = null;
        if (Psh1126UseAnyAsyncOverCountAsyncAnalyzer.TryGetComparisonShape(binary) is not { } shape
            || shape.Invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var anyAsyncCall = shape.Invocation
            .WithExpression(memberAccess.WithName(SyntaxFactory.IdentifierName(Psh1126UseAnyAsyncOverCountAsyncAnalyzer.AnyAsyncMethodName)))
            .WithoutTrivia();

        if (!BindsToAnySibling(model, binary.SpanStart, anyAsyncCall))
        {
            return false;
        }

        ExpressionSyntax result = SyntaxFactory.AwaitExpression(
            SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            anyAsyncCall);
        if (!shape.HasElements)
        {
            result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, result);
        }

        replacement = result.WithTriviaFrom(binary).WithAdditionalAnnotations(Formatter.Annotation);
        return true;
    }

    /// <summary>Speculatively binds the rewritten call and confirms it resolves to a boolean-returning AnyAsync.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The original comparison's position, used as the speculative binding context.</param>
    /// <param name="candidate">The rewritten AnyAsync invocation.</param>
    /// <returns><see langword="true"/> when the replacement binds to an AnyAsync awaiting to bool.</returns>
    private static bool BindsToAnySibling(SemanticModel model, int position, InvocationExpressionSyntax candidate)
        => model.GetSpeculativeSymbolInfo(position, candidate, SpeculativeBindingOption.BindAsExpression).Symbol
                is IMethodSymbol { Name: Psh1126UseAnyAsyncOverCountAsyncAnalyzer.AnyAsyncMethodName, ReturnType: INamedTypeSymbol { TypeArguments.Length: 1 } returnType }
            && returnType.TypeArguments[0].SpecialType == SpecialType.System_Boolean;
}
