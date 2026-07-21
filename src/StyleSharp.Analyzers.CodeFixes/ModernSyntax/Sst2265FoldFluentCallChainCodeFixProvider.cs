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
public sealed class Sst2265FoldFluentCallChainCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.FoldFluentCallChain.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (Resolve(root, model, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Fold the calls into a fluent chain",
                    _ => Task.FromResult(Apply(context.Document, root, edit)),
                    equivalenceKey: nameof(Sst2265FoldFluentCallChainCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, editor.SemanticModel, diagnostic) is not { } edit)
        {
            return;
        }

        editor.ReplaceNode(edit.First, edit.Folded);
        foreach (var statement in edit.Rest)
        {
            editor.RemoveNode(statement);
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
        var statements = block.Statements.Replace(edit.First, edit.Folded);
        for (var i = 0; i < edit.Rest.Length; i++)
        {
            statements = statements.RemoveAt(firstIndex + 1);
        }

        return document.WithSyntaxRoot(root.ReplaceNode(block, block.WithStatements(statements)));
    }

    /// <summary>Resolves the reported run into the first statement, the rest, and the folded statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The edit, or <see langword="null"/> when the shape no longer matches.</returns>
    private static FoldEdit? Resolve(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ExpressionStatementSyntax>() is not { Parent: BlockSyntax block } first)
        {
            return null;
        }

        var index = block.Statements.IndexOf(first);
        var count = index < 0 ? 0 : Sst2265FoldFluentCallChainAnalyzer.CountFluentRun(model, block, index);
        if (count < Sst2265FoldFluentCallChainAnalyzer.MinimumRunLength)
        {
            return null;
        }

        var rest = new StatementSyntax[count - 1];
        for (var i = 1; i < count; i++)
        {
            rest[i - 1] = block.Statements[index + i];
        }

        return new FoldEdit(first, rest, BuildFolded(block, index, count));
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
        ExpressionSyntax accumulated = first.Expression.WithoutTrivia();
        for (var i = index + 1; i < index + count; i++)
        {
            var invocation = (InvocationExpressionSyntax)((ExpressionStatementSyntax)block.Statements[i]).Expression;
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            accumulated = invocation.WithExpression(memberAccess.WithExpression(accumulated));
        }

        return SyntaxFactory.ExpressionStatement(accumulated.WithoutTrivia())
            .WithLeadingTrivia(first.GetLeadingTrivia())
            .WithTrailingTrivia(last.GetTrailingTrivia());
    }

    /// <summary>The first statement to replace, the statements to drop, and the folded replacement.</summary>
    /// <param name="First">The first statement of the run, replaced by the folded chain.</param>
    /// <param name="Rest">The remaining statements of the run, removed.</param>
    /// <param name="Folded">The single chained statement.</param>
    internal readonly record struct FoldEdit(
        ExpressionStatementSyntax First,
        StatementSyntax[] Rest,
        ExpressionStatementSyntax Folded);
}
