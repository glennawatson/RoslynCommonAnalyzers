// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a trailing rethrow-only catch clause (SST1470). When the try statement keeps other
/// catch clauses or a finally clause, only the reported clause is deleted; when the clause is a
/// bare try/catch's only handler, the whole try statement is replaced by the try block's
/// statements, spliced into the parent block where the grammar allows it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1470RemoveRethrowOnlyCatchCodeFixProvider))]
[Shared]
public sealed class Sst1470RemoveRethrowOnlyCatchCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.RemoveRethrowOnlyCatch.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!TryGetCatchClause(root, diagnostic, out var catchClause))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the rethrow-only catch clause",
                    _ => Task.FromResult(Apply(context.Document, root, catchClause!)),
                    equivalenceKey: nameof(Sst1470RemoveRethrowOnlyCatchCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetCatchClause(editor.OriginalRoot, diagnostic, out var catchClause)
            || catchClause!.Parent is not TryStatementSyntax tryStatement
            || IsInsideUnwrappedTryBlock(tryStatement))
        {
            return;
        }

        if (tryStatement.Catches.Count > 1 || tryStatement.Finally is not null)
        {
            editor.ReplaceNode(tryStatement, (current, _) => RemoveLastCatch(current));
            return;
        }

        RegisterUnwrapEdits(editor, tryStatement);
    }

    /// <summary>Applies the fix for one rethrow-only catch clause.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="catchClause">The rethrow-only catch clause.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, CatchClauseSyntax catchClause)
    {
        if (catchClause.Parent is not TryStatementSyntax tryStatement)
        {
            return document;
        }

        if (tryStatement.Catches.Count > 1 || tryStatement.Finally is not null)
        {
            var updated = tryStatement.WithCatches(tryStatement.Catches.Remove(catchClause));
            return document.WithSyntaxRoot(root.ReplaceNode(tryStatement, updated));
        }

        return document.WithSyntaxRoot(UnwrapTryStatement(root, tryStatement));
    }

    /// <summary>Resolves the diagnostic's span to its trailing rethrow-only catch clause.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="catchClause">The reported catch clause when found.</param>
    /// <returns><see langword="true"/> when the reported shape still matches.</returns>
    private static bool TryGetCatchClause(SyntaxNode root, Diagnostic diagnostic, out CatchClauseSyntax? catchClause)
    {
        catchClause = root.FindNode(diagnostic.Location.SourceSpan) as CatchClauseSyntax;
        return catchClause?.Parent is TryStatementSyntax tryStatement
            && tryStatement.Catches[tryStatement.Catches.Count - 1] == catchClause
            && Sst1470RemoveRethrowOnlyCatchAnalyzer.IsRethrowOnly(catchClause);
    }

    /// <summary>Removes the last catch clause from a try statement that keeps other clauses.</summary>
    /// <param name="node">The current try statement, including any nested batch edits.</param>
    /// <returns>The try statement without its trailing catch clause.</returns>
    private static SyntaxNode RemoveLastCatch(SyntaxNode node)
    {
        if (node is not TryStatementSyntax tryStatement || tryStatement.Catches.Count == 0)
        {
            return node;
        }

        return tryStatement.WithCatches(tryStatement.Catches.RemoveAt(tryStatement.Catches.Count - 1));
    }

    /// <summary>Replaces a bare try/catch with the statements of its try block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="tryStatement">The try statement to unwrap.</param>
    /// <returns>The updated root.</returns>
    private static SyntaxNode UnwrapTryStatement(SyntaxNode root, TryStatementSyntax tryStatement)
    {
        var hoisted = BuildHoistedStatements(tryStatement);
        return tryStatement.Parent switch
        {
            BlockSyntax or SwitchSectionSyntax => hoisted.Count == 0
                ? root.RemoveNode(tryStatement, SyntaxRemoveOptions.KeepUnbalancedDirectives) ?? root
                : root.ReplaceNode(tryStatement, hoisted),
            GlobalStatementSyntax globalStatement => hoisted.Count == 0
                ? root.RemoveNode(globalStatement, SyntaxRemoveOptions.KeepUnbalancedDirectives) ?? root
                : root.ReplaceNode(globalStatement, WrapAsGlobalStatements(hoisted)),
            _ => root.ReplaceNode(tryStatement, BuildEmbeddedReplacement(tryStatement)),
        };
    }

    /// <summary>Registers the batch edits that unwrap a bare try/catch.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="tryStatement">The try statement to unwrap.</param>
    private static void RegisterUnwrapEdits(DocumentEditor editor, TryStatementSyntax tryStatement)
    {
        var hoisted = BuildHoistedStatements(tryStatement);
        switch (tryStatement.Parent)
        {
            case BlockSyntax or SwitchSectionSyntax:
            {
                if (hoisted.Count > 0)
                {
                    editor.InsertBefore(tryStatement, hoisted);
                }

                editor.RemoveNode(tryStatement, SyntaxRemoveOptions.KeepUnbalancedDirectives);
                break;
            }

            case GlobalStatementSyntax globalStatement:
            {
                if (hoisted.Count > 0)
                {
                    editor.InsertBefore(globalStatement, WrapAsGlobalStatements(hoisted));
                }

                editor.RemoveNode(globalStatement, SyntaxRemoveOptions.KeepUnbalancedDirectives);
                break;
            }

            default:
            {
                editor.ReplaceNode(tryStatement, BuildEmbeddedReplacement(tryStatement));
                break;
            }
        }
    }

    /// <summary>Builds the try block's statements ready to take the try statement's place in a statement list.</summary>
    /// <param name="tryStatement">The try statement being unwrapped.</param>
    /// <returns>The hoisted statements, with the try's leading trivia on the first one.</returns>
    private static SyntaxList<StatementSyntax> BuildHoistedStatements(TryStatementSyntax tryStatement)
    {
        var statements = tryStatement.Block.Statements;
        var count = statements.Count;
        if (count == 0)
        {
            return statements;
        }

        var hoisted = new StatementSyntax[count];
        for (var i = 0; i < count; i++)
        {
            hoisted[i] = statements[i].WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
        }

        hoisted[0] = hoisted[0].WithLeadingTrivia(tryStatement.GetLeadingTrivia());
        return SyntaxFactory.List(hoisted);
    }

    /// <summary>Builds the single-statement replacement used where the parent is not a statement list.</summary>
    /// <param name="tryStatement">The try statement being unwrapped.</param>
    /// <returns>The lone hoisted statement, or the try's own block when a block is syntactically required.</returns>
    private static StatementSyntax BuildEmbeddedReplacement(TryStatementSyntax tryStatement)
    {
        var statements = tryStatement.Block.Statements;
        var replacement = statements.Count == 1 && CanStandWithoutBlock(statements[0])
            ? statements[0]
            : (StatementSyntax)tryStatement.Block;
        return replacement
            .WithLeadingTrivia(tryStatement.GetLeadingTrivia())
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Returns whether a statement is valid outside a block in an embedded-statement position.</summary>
    /// <param name="statement">The candidate statement.</param>
    /// <returns><see langword="true"/> when no wrapping block is required.</returns>
    private static bool CanStandWithoutBlock(StatementSyntax statement)
        => statement is not LocalDeclarationStatementSyntax and not LocalFunctionStatementSyntax and not LabeledStatementSyntax;

    /// <summary>Wraps hoisted statements as top-level global statements.</summary>
    /// <param name="statements">The hoisted statements.</param>
    /// <returns>The wrapped global statements.</returns>
    private static GlobalStatementSyntax[] WrapAsGlobalStatements(SyntaxList<StatementSyntax> statements)
    {
        var wrapped = new GlobalStatementSyntax[statements.Count];
        for (var i = 0; i < statements.Count; i++)
        {
            wrapped[i] = SyntaxFactory.GlobalStatement(statements[i]);
        }

        return wrapped;
    }

    /// <summary>
    /// Returns whether an ancestor try statement will itself be unwrapped by this fix. A batch pass
    /// splices that ancestor's block statements from the original tree, so an edit registered inside
    /// it could not be tracked; the inner clause is skipped and picked up by the next fix pass.
    /// </summary>
    /// <param name="tryStatement">The try statement whose ancestors are checked.</param>
    /// <returns><see langword="true"/> when an enclosing bare try/catch is also rethrow-only.</returns>
    private static bool IsInsideUnwrappedTryBlock(TryStatementSyntax tryStatement)
    {
        var node = tryStatement.Parent;
        while (node is not null)
        {
            if (node is TryStatementSyntax ancestor && IsUnwrappableTry(ancestor))
            {
                return true;
            }

            node = node.Parent;
        }

        return false;
    }

    /// <summary>Returns whether a try statement is a bare try/catch whose only clause is rethrow-only.</summary>
    /// <param name="tryStatement">The try statement.</param>
    /// <returns><see langword="true"/> when the fix would unwrap the whole try statement.</returns>
    private static bool IsUnwrappableTry(TryStatementSyntax tryStatement)
        => tryStatement.Finally is null
            && tryStatement.Catches.Count == 1
            && Sst1470RemoveRethrowOnlyCatchAnalyzer.IsRethrowOnly(tryStatement.Catches[0]);
}
