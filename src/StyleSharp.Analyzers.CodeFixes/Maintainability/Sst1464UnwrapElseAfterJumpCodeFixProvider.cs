// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an SST1464 else clause and hoists its statements to directly follow the if statement.
/// The fix is offered only when unwrapping cannot change scoping: the if statement sits directly
/// inside a block, and either the else introduces no local declarations or the if statement is
/// already the last statement of that block. Otherwise the diagnostic stays fix-less.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1464UnwrapElseAfterJumpCodeFixProvider))]
[Shared]
public sealed class Sst1464UnwrapElseAfterJumpCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UnwrapElseAfterJump.Id);

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
            if (!TryFindTarget(root, diagnostic, out _, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Unwrap the else clause",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst1464UnwrapElseAfterJumpCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryFindTarget(editor.OriginalRoot, diagnostic, out var block, out var ifStatement)
            || block is null
            || ifStatement is null)
        {
            return;
        }

        var index = block.Statements.IndexOf(ifStatement);
        editor.ReplaceNode(block, (current, _) => current is BlockSyntax currentBlock ? Unwrap(currentBlock, index) : current);
    }

    /// <summary>Removes the reported else clause and hoists its statements after the if statement.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original document when the diagnostic no longer resolves.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryFindTarget(root, diagnostic, out var block, out var ifStatement)
            || block is null
            || ifStatement is null)
        {
            return document;
        }

        var updated = Unwrap(block, block.Statements.IndexOf(ifStatement));
        return document.WithSyntaxRoot(root.ReplaceNode(block, updated));
    }

    /// <summary>Resolves the reported else clause to a fixable if statement and its containing block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="block">The block containing the if statement.</param>
    /// <param name="ifStatement">The if statement whose else clause is unwrapped.</param>
    /// <returns><see langword="true"/> when the else clause can be unwrapped safely.</returns>
    private static bool TryFindTarget(SyntaxNode root, Diagnostic diagnostic, out BlockSyntax? block, out IfStatementSyntax? ifStatement)
    {
        block = null;
        ifStatement = null;
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent
                is not ElseClauseSyntax { Parent: IfStatementSyntax { Parent: BlockSyntax containingBlock } target } elseClause
            || !Sst1464UnwrapElseAfterJumpAnalyzer.BranchAlwaysJumps(target.Statement)
            || !IsScopeSafe(containingBlock, containingBlock.Statements.IndexOf(target), elseClause))
        {
            return false;
        }

        block = containingBlock;
        ifStatement = target;
        return true;
    }

    /// <summary>Rebuilds a block with the if statement at the given index unwrapped.</summary>
    /// <param name="block">The block containing the if statement.</param>
    /// <param name="index">The if statement's index in the block.</param>
    /// <returns>The updated block, or the original block when the shape no longer matches.</returns>
    private static BlockSyntax Unwrap(BlockSyntax block, int index)
    {
        var statements = block.Statements;
        if (index < 0
            || index >= statements.Count
            || statements[index] is not IfStatementSyntax { Else: { } elseClause } ifStatement
            || !Sst1464UnwrapElseAfterJumpAnalyzer.BranchAlwaysJumps(ifStatement.Statement)
            || !IsScopeSafe(block, index, elseClause))
        {
            return block;
        }

        var updated = statements.Replace(ifStatement, WithTrailingNewLine(ifStatement.WithElse(null)));
        updated = updated.InsertRange(index + 1, GetHoistedStatements(elseClause));
        return block.WithStatements(updated);
    }

    /// <summary>Returns whether unwrapping the else clause cannot change what its declarations scope over.</summary>
    /// <param name="block">The block containing the if statement.</param>
    /// <param name="index">The if statement's index in the block.</param>
    /// <param name="elseClause">The else clause being unwrapped.</param>
    /// <returns><see langword="true"/> when the else declares nothing or no statements follow the if.</returns>
    private static bool IsScopeSafe(BlockSyntax block, int index, ElseClauseSyntax elseClause)
        => !DeclaresLocals(elseClause.Statement) || index == block.Statements.Count - 1;

    /// <summary>Returns whether an else body directly declares locals or local functions.</summary>
    /// <param name="statement">The else clause's statement.</param>
    /// <returns><see langword="true"/> when hoisting would move a declaration into the outer scope.</returns>
    private static bool DeclaresLocals(StatementSyntax statement)
    {
        if (statement is not BlockSyntax block)
        {
            return false;
        }

        var statements = block.Statements;
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Collects the statements to hoist after the if statement, annotated for formatting.</summary>
    /// <param name="elseClause">The else clause being unwrapped.</param>
    /// <returns>The hoisted statements: block contents spliced, anything else (including an else-if) whole.</returns>
    private static StatementSyntax[] GetHoistedStatements(ElseClauseSyntax elseClause)
    {
        if (elseClause.Statement is not BlockSyntax elseBlock)
        {
            return [elseClause.Statement.WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation)];
        }

        var source = elseBlock.Statements;
        var hoisted = new StatementSyntax[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            hoisted[i] = source[i].WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
        }

        return hoisted;
    }

    /// <summary>Ensures the unwrapped if statement ends its line before the hoisted statements.</summary>
    /// <param name="statement">The if statement with its else clause removed.</param>
    /// <returns>The statement with a trailing end of line.</returns>
    private static IfStatementSyntax WithTrailingNewLine(IfStatementSyntax statement)
    {
        var trailing = statement.GetTrailingTrivia();
        for (var i = 0; i < trailing.Count; i++)
        {
            if (trailing[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return statement;
            }
        }

        return statement.WithTrailingTrivia(trailing.Add(SyntaxFactory.ElasticCarriageReturnLineFeed));
    }
}
