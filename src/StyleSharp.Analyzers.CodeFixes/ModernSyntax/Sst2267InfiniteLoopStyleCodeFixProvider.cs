// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an infinite loop into the configured style (SST2267): a <c>for (;;)</c> becomes
/// <c>while (true)</c>, and a <c>while (true)</c> becomes <c>for (;;)</c>. The direction follows the
/// reported node's kind — the analyzer only reports the form that does not match the configured style —
/// so the fix reuses the loop's own keyword trivia, parentheses, and body, moving nothing else.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2267InfiniteLoopStyleCodeFixProvider))]
[Shared]
public sealed class Sst2267InfiniteLoopStyleCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.NormalizeInfiniteLoopStyle.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Normalize the infinite loop style", nameof(Sst2267InfiniteLoopStyleCodeFixProvider), CanRewrite, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        FindForeverLoop(root, diagnostic) is not null;

    /// <summary>Resolves the reported loop and builds its opposite-style replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        FindForeverLoop(root, diagnostic) switch
        {
            ForStatementSyntax forStatement => new NodeReplacement(forStatement, ToWhile(forStatement), RewriteCurrent),
            WhileStatementSyntax whileStatement => new NodeReplacement(whileStatement, ToFor(whileStatement), RewriteCurrent),
            _ => null,
        };

    /// <summary>Finds the infinite loop the diagnostic was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The <c>for</c> or <c>while</c> loop, or <see langword="null"/> when the nearest statement is not one that runs forever.</returns>
    private static StatementSyntax? FindForeverLoop(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case ForStatementSyntax forStatement when Sst2267InfiniteLoopStyleAnalyzer.IsForeverFor(forStatement):
                    return forStatement;
                case WhileStatementSyntax whileStatement when Sst2267InfiniteLoopStyleAnalyzer.IsForeverWhile(whileStatement):
                    return whileStatement;
                case StatementSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Rewrites the current loop during batch FixAll composition.</summary>
    /// <param name="current">The current loop node, possibly carrying nested edits.</param>
    /// <returns>The flipped loop, or the node unchanged when the shape no longer matches.</returns>
    private static SyntaxNode RewriteCurrent(SyntaxNode current) => current switch
    {
        ForStatementSyntax forStatement when Sst2267InfiniteLoopStyleAnalyzer.IsForeverFor(forStatement) => ToWhile(forStatement),
        WhileStatementSyntax whileStatement when Sst2267InfiniteLoopStyleAnalyzer.IsForeverWhile(whileStatement) => ToFor(whileStatement),
        _ => current,
    };

    /// <summary>Builds the <c>while (true)</c> form of a <c>for (;;)</c> loop.</summary>
    /// <param name="statement">The reported loop; callers must have validated the shape.</param>
    /// <returns>The rewritten loop.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static WhileStatementSyntax ToWhile(ForStatementSyntax statement) =>
        SyntaxFactory.WhileStatement(
            statement.AttributeLists,
            SyntaxFactory.Token(statement.ForKeyword.LeadingTrivia, SyntaxKind.WhileKeyword, statement.ForKeyword.TrailingTrivia),
            statement.OpenParenToken,
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression),
            statement.CloseParenToken,
            statement.Statement);

    /// <summary>Builds the <c>for (;;)</c> form of a <c>while (true)</c> loop.</summary>
    /// <param name="statement">The reported loop; callers must have validated the shape.</param>
    /// <returns>The rewritten loop.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ForStatementSyntax ToFor(WhileStatementSyntax statement) =>
        SyntaxFactory.ForStatement(
            statement.AttributeLists,
            SyntaxFactory.Token(statement.WhileKeyword.LeadingTrivia, SyntaxKind.ForKeyword, statement.WhileKeyword.TrailingTrivia),
            statement.OpenParenToken,
            declaration: null,
            default,
            SyntaxFactory.Token(SyntaxKind.SemicolonToken),
            condition: null,
            SyntaxFactory.Token(SyntaxKind.SemicolonToken),
            default,
            statement.CloseParenToken,
            statement.Statement);
}
