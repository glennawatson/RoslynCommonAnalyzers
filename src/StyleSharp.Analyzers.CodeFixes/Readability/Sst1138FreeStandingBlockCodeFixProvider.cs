// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Splices a free-standing block's statements into its enclosing block (SST1138). Because the block
/// declares nothing, moving its statements out one level changes no scoping.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1138FreeStandingBlockCodeFixProvider))]
[Shared]
public sealed class Sst1138FreeStandingBlockCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.FreeStandingBlock.Id);

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
            if (Resolve(root, diagnostic) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Splice the block's statements into the enclosing block",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst1138FreeStandingBlockCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not { Parent: BlockSyntax parent } block)
        {
            return;
        }

        var index = parent.Statements.IndexOf(block);
        editor.ReplaceNode(parent, (current, _) => current is BlockSyntax currentBlock ? Splice(currentBlock, index) : current);
    }

    /// <summary>Splices the reported block into its parent.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original when the shape no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (Resolve(root, diagnostic) is not { Parent: BlockSyntax parent } block)
        {
            return document;
        }

        var updated = Splice(parent, parent.Statements.IndexOf(block));
        return document.WithSyntaxRoot(root.ReplaceNode(parent, updated));
    }

    /// <summary>Resolves the reported free-standing block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The block, or <see langword="null"/> when the shape no longer matches.</returns>
    private static BlockSyntax? Resolve(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is BlockSyntax { Parent: BlockSyntax } block ? block : null;

    /// <summary>Rebuilds a parent block with the child block at the given index spliced in.</summary>
    /// <param name="parent">The enclosing block.</param>
    /// <param name="index">The free-standing block's index in the parent.</param>
    /// <returns>The updated block, or the original when the shape no longer matches.</returns>
    private static BlockSyntax Splice(BlockSyntax parent, int index)
    {
        if (index < 0 || index >= parent.Statements.Count || parent.Statements[index] is not BlockSyntax block)
        {
            return parent;
        }

        var inner = block.Statements;
        var hoisted = new StatementSyntax[inner.Count];
        for (var i = 0; i < inner.Count; i++)
        {
            hoisted[i] = inner[i].WithAdditionalAnnotations(Formatter.Annotation);
        }

        return parent.WithStatements(parent.Statements.RemoveAt(index).InsertRange(index, hoisted));
    }
}
