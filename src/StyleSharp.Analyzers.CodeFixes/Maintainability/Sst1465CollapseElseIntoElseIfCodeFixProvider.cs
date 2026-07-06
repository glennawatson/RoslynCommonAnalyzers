// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces an else block that only wraps an if statement with the if statement itself, producing
/// an <c>else if</c> chain (SST1465). The inner if keeps its own else chain untouched, and comment
/// trivia attached inside the removed braces is merged onto the hoisted if.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1465CollapseElseIntoElseIfCodeFixProvider))]
[Shared]
public sealed class Sst1465CollapseElseIntoElseIfCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.CollapseElseIntoElseIf.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Collapse to 'else if'", nameof(Sst1465CollapseElseIntoElseIfCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies one SST1465 collapse for the reported else clause.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original document when the diagnostic no longer resolves.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => TryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Resolves the reported else clause and builds its collapsed <c>else if</c> form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ElseClauseSyntax>() is { } elseClause && IsCollapsible(elseClause)
            ? new NodeReplacement(
                elseClause,
                Collapse(elseClause),
                static current => current is ElseClauseSyntax currentElse && IsCollapsible(currentElse) ? Collapse(currentElse) : current)
            : null;

    /// <summary>Returns whether an else clause still wraps exactly one if statement that is safe to hoist.</summary>
    /// <param name="elseClause">The else clause to inspect.</param>
    /// <returns><see langword="true"/> when the collapse can be applied mechanically.</returns>
    private static bool IsCollapsible(ElseClauseSyntax elseClause)
        => elseClause.Statement is BlockSyntax block
            && block.Statements.Count == 1
            && block.Statements[0] is IfStatementSyntax innerIf
            && !HasDirective(elseClause.ElseKeyword.TrailingTrivia)
            && !HasDirective(block.OpenBraceToken.LeadingTrivia)
            && !HasDirective(block.OpenBraceToken.TrailingTrivia)
            && !HasDirective(innerIf.GetLeadingTrivia())
            && !HasDirective(innerIf.GetTrailingTrivia())
            && !HasDirective(block.CloseBraceToken.LeadingTrivia)
            && !HasDirective(block.CloseBraceToken.TrailingTrivia);

    /// <summary>Builds the collapsed else clause with the inner if hoisted out of its block.</summary>
    /// <param name="elseClause">The else clause to collapse.</param>
    /// <returns>The collapsed else clause.</returns>
    private static ElseClauseSyntax Collapse(ElseClauseSyntax elseClause)
    {
        var block = (BlockSyntax)elseClause.Statement;
        var innerIf = (IfStatementSyntax)block.Statements[0];
        var hoisted = innerIf
            .WithLeadingTrivia(BuildLeadingTrivia(elseClause, block, innerIf))
            .WithTrailingTrivia(BuildTrailingTrivia(block, innerIf))
            .WithAdditionalAnnotations(Formatter.Annotation);
        return elseClause
            .WithElseKeyword(elseClause.ElseKeyword.WithTrailingTrivia(SyntaxFactory.Space))
            .WithStatement(hoisted);
    }

    /// <summary>Merges comment trivia that preceded the inner if onto the hoisted if's leading trivia.</summary>
    /// <param name="elseClause">The else clause being collapsed.</param>
    /// <param name="block">The block being removed.</param>
    /// <param name="innerIf">The if statement being hoisted.</param>
    /// <returns>The leading trivia for the hoisted if.</returns>
    private static SyntaxTriviaList BuildLeadingTrivia(ElseClauseSyntax elseClause, BlockSyntax block, IfStatementSyntax innerIf)
    {
        var pieces = new List<SyntaxTrivia>();
        AppendComments(pieces, elseClause.ElseKeyword.TrailingTrivia);
        AppendComments(pieces, block.OpenBraceToken.LeadingTrivia);
        AppendComments(pieces, block.OpenBraceToken.TrailingTrivia);
        AppendComments(pieces, innerIf.GetLeadingTrivia());
        return SyntaxFactory.TriviaList(pieces);
    }

    /// <summary>Merges comment trivia that trailed the inner if onto the hoisted if's trailing trivia.</summary>
    /// <param name="block">The block being removed.</param>
    /// <param name="innerIf">The if statement being hoisted.</param>
    /// <returns>The trailing trivia for the hoisted if.</returns>
    private static SyntaxTriviaList BuildTrailingTrivia(BlockSyntax block, IfStatementSyntax innerIf)
    {
        var pieces = new List<SyntaxTrivia>();
        AppendAll(pieces, innerIf.GetTrailingTrivia());
        AppendComments(pieces, block.CloseBraceToken.LeadingTrivia);
        AppendAll(pieces, block.CloseBraceToken.TrailingTrivia);
        return Normalize(pieces);
    }

    /// <summary>Appends the comments in a trivia list, each terminated the way it was in source.</summary>
    /// <param name="pieces">The destination trivia buffer.</param>
    /// <param name="trivia">The trivia list to scan.</param>
    private static void AppendComments(List<SyntaxTrivia> pieces, SyntaxTriviaList trivia)
    {
        var index = 0;
        while (index < trivia.Count)
        {
            var current = trivia[index];
            if (!IsComment(current))
            {
                index++;
                continue;
            }

            pieces.Add(current);
            if (index + 1 < trivia.Count && trivia[index + 1].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                pieces.Add(trivia[index + 1]);
                index++;
            }
            else if (current.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                pieces.Add(SyntaxFactory.Space);
            }
            else
            {
                pieces.Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
            }

            index++;
        }
    }

    /// <summary>Appends every trivia in a list to the buffer.</summary>
    /// <param name="pieces">The destination trivia buffer.</param>
    /// <param name="trivia">The trivia list to copy.</param>
    private static void AppendAll(List<SyntaxTrivia> pieces, SyntaxTriviaList trivia)
    {
        for (var i = 0; i < trivia.Count; i++)
        {
            pieces.Add(trivia[i]);
        }
    }

    /// <summary>Drops redundant line breaks and dangling indentation left by the removed braces.</summary>
    /// <param name="pieces">The merged trivia buffer.</param>
    /// <returns>The normalized trivia list.</returns>
    private static SyntaxTriviaList Normalize(List<SyntaxTrivia> pieces)
    {
        var result = new List<SyntaxTrivia>(pieces.Count);
        var pendingWhitespaceIndex = -1;
        for (var i = 0; i < pieces.Count; i++)
        {
            var trivia = pieces[i];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                pendingWhitespaceIndex = i;
                continue;
            }

            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                pendingWhitespaceIndex = -1;
                if (result.Count == 0 || !result[result.Count - 1].IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    result.Add(trivia);
                }

                continue;
            }

            if (pendingWhitespaceIndex >= 0)
            {
                result.Add(pieces[pendingWhitespaceIndex]);
                pendingWhitespaceIndex = -1;
            }

            result.Add(trivia);
        }

        return SyntaxFactory.TriviaList(result);
    }

    /// <summary>Returns whether a trivia is a comment worth carrying through the collapse.</summary>
    /// <param name="trivia">The trivia to inspect.</param>
    /// <returns><see langword="true"/> for comment trivia.</returns>
    private static bool IsComment(SyntaxTrivia trivia)
        => trivia.Kind() is
            SyntaxKind.SingleLineCommentTrivia or
            SyntaxKind.MultiLineCommentTrivia or
            SyntaxKind.SingleLineDocumentationCommentTrivia or
            SyntaxKind.MultiLineDocumentationCommentTrivia;

    /// <summary>Returns whether a trivia list carries a preprocessor directive the collapse would disturb.</summary>
    /// <param name="trivia">The trivia list to scan.</param>
    /// <returns><see langword="true"/> when a directive is present.</returns>
    private static bool HasDirective(SyntaxTriviaList trivia)
    {
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsDirective)
            {
                return true;
            }
        }

        return false;
    }
}
