// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Folds a run of consecutive fluent calls on one receiver into a single chained statement (SST2265):
/// <c>b.Append(a); b.Append(c);</c> becomes <c>b.Append(a).Append(c);</c>. Each later call's receiver is
/// re-parented onto the growing chain, the first statement keeps its leading trivia, the last keeps its
/// trailing trivia, and the intervening statements are removed.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2265FoldFluentCallChainCodeFixProvider))]
[Shared]
public sealed class Sst2265FoldFluentCallChainCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.FoldFluentCallChain.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync<FoldEdit>(
            context,
            "Fold the calls into a fluent chain",
            nameof(Sst2265FoldFluentCallChainCodeFixProvider),
            TryResolve,
            Apply);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryResolve(editor.OriginalRoot, editor.SemanticModel, diagnostic, CancellationToken.None, out var edit))
        {
            return;
        }

        var block = (BlockSyntax)edit.First.Parent!;
        var index = block.Statements.IndexOf(edit.First);
        editor.ReplaceNode(edit.First, BuildFolded(block, index, edit.Count));
        for (var i = 1; i < edit.Count; i++)
        {
            editor.RemoveNode(block.Statements[index + i]);
        }
    }

    /// <summary>Applies one fold by replacing the first statement and dropping the rest of the run.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="edit">The resolved edit.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, FoldEdit edit)
    {
        var block = (BlockSyntax)edit.First.Parent!;
        var firstIndex = block.Statements.IndexOf(edit.First);
        var statements = block.Statements.Replace(edit.First, BuildFolded(block, firstIndex, edit.Count));
        for (var i = 1; i < edit.Count; i++)
        {
            statements = statements.RemoveAt(firstIndex + 1);
        }

        return document.WithSyntaxRoot(root.ReplaceNode(block, block.Update(block.AttributeLists, block.OpenBraceToken, statements, block.CloseBraceToken)));
    }

    /// <summary>Resolves the original run without allocating a replacement or a removal array.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="edit">The resolved edit.</param>
    /// <returns><see langword="true"/> when the shape still matches.</returns>
    private static bool TryResolve(SyntaxNode root, SemanticModel model, Diagnostic diagnostic, CancellationToken cancellationToken, out FoldEdit edit)
    {
        edit = default;
        cancellationToken.ThrowIfCancellationRequested();
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ExpressionStatementSyntax>() is not { Parent: BlockSyntax block } first)
        {
            return false;
        }

        var index = block.Statements.IndexOf(first);
        var count = index < 0 ? 0 : Sst2265FoldFluentCallChainAnalyzer.CountFluentRun(model, block, index);
        if (count < Sst2265FoldFluentCallChainAnalyzer.MinimumRunLength)
        {
            return false;
        }

        // The run collapses into its first statement and the rest are deleted, so a directive anywhere
        // across it would lose whichever half sits on a statement that goes.
        if (DirectiveBoundaries.Separate(first, block.Statements[index + count - 1]))
        {
            return false;
        }

        edit = new(first, count);
        return true;
    }

    /// <summary>Builds the single chained statement replacing a fluent-call run.</summary>
    /// <param name="block">The block holding the run.</param>
    /// <param name="index">The index of the first statement in the run.</param>
    /// <param name="count">The number of statements in the run.</param>
    /// <returns>The folded statement.</returns>
    private static ExpressionStatementSyntax BuildFolded(BlockSyntax block, int index, int count)
    {
        var first = (ExpressionStatementSyntax)block.Statements[index];
        var last = (ExpressionStatementSyntax)block.Statements[index + count - 1];
        var accumulated = first.Expression.WithoutTrivia();
        for (var i = index + 1; i < index + count; i++)
        {
            var invocation = (InvocationExpressionSyntax)((ExpressionStatementSyntax)block.Statements[i]).Expression;
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            accumulated = invocation.Update(
                memberAccess.Update(accumulated, memberAccess.OperatorToken, memberAccess.Name),
                invocation.ArgumentList);
        }

        var foldedInvocation = (InvocationExpressionSyntax)accumulated;
        return SyntaxFactory.ExpressionStatement(
            default,
            foldedInvocation.Update(
                foldedInvocation.Expression.WithLeadingTrivia(first.GetLeadingTrivia()),
                foldedInvocation.ArgumentList.WithoutTrailingTrivia()),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.SemicolonToken, last.GetTrailingTrivia()));
    }

    /// <summary>The original run, retained without constructing its folded replacement.</summary>
    /// <param name="First">The first statement of the run, replaced by the folded chain.</param>
    /// <param name="Count">The number of statements to fold.</param>
    internal readonly record struct FoldEdit(
        ExpressionStatementSyntax First,
        int Count);
}
