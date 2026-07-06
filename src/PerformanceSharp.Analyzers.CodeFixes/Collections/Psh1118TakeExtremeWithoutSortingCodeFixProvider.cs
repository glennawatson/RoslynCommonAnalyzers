// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a sort-then-take-one chain to a single extreme scan (PSH1118):
/// <c>OrderBy(k).First()</c> becomes <c>MinBy(k)</c>, <c>OrderBy(k).Last()</c> becomes
/// <c>MaxBy(k)</c>, the descending forms map the other way around, and an identity selector
/// collapses to <c>Min()</c>/<c>Max()</c> with the selector dropped. The chain's outer trivia
/// is preserved and the replacement carries the formatter annotation.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1118TakeExtremeWithoutSortingCodeFixProvider))]
[Shared]
public sealed class Psh1118TakeExtremeWithoutSortingCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.TakeExtremeWithoutSorting.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Take the extreme element directly", nameof(Psh1118TakeExtremeWithoutSortingCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported chain with its extreme-scan form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported terminal invocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)));

    /// <summary>Resolves the reported chain and builds its extreme-scan replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => TryGetTerminalInvocation(root, diagnostic) is { } invocation
            ? new NodeReplacement(invocation, Rewrite(invocation), RewriteCurrent)
            : null;

    /// <summary>Rewrites the current chain during batch FixAll composition.</summary>
    /// <param name="current">The current chain node, possibly carrying nested edits.</param>
    /// <returns>The rewritten chain, or the node unchanged when the shape no longer matches.</returns>
    private static SyntaxNode RewriteCurrent(SyntaxNode current)
        => current is InvocationExpressionSyntax invocation && Psh1118TakeExtremeWithoutSortingAnalyzer.IsExtremeChainShape(invocation)
            ? Rewrite(invocation)
            : current;

    /// <summary>Returns the reported terminal invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetTerminalInvocation(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return Psh1118TakeExtremeWithoutSortingAnalyzer.IsExtremeChainShape(invocation) ? invocation : null;
            }
        }

        return null;
    }

    /// <summary>Builds the extreme-scan invocation, dropping the terminal call.</summary>
    /// <param name="invocation">The terminal invocation to rewrite; callers must have validated the shape.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var terminal = (MemberAccessExpressionSyntax)invocation.Expression;
        var sort = (InvocationExpressionSyntax)terminal.Expression;
        var sortAccess = (MemberAccessExpressionSyntax)sort.Expression;
        var replacementName = Psh1118TakeExtremeWithoutSortingAnalyzer.GetReplacementName(invocation);
        var newName = SyntaxFactory.IdentifierName(replacementName).WithTriviaFrom(sortAccess.Name);

        var rewritten = sort.WithExpression(sortAccess.WithName(newName));
        if (replacementName is Psh1118TakeExtremeWithoutSortingAnalyzer.MinMethodName or Psh1118TakeExtremeWithoutSortingAnalyzer.MaxMethodName)
        {
            rewritten = rewritten.WithArgumentList(sort.ArgumentList.WithArguments(default));
        }

        return rewritten
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }
}
