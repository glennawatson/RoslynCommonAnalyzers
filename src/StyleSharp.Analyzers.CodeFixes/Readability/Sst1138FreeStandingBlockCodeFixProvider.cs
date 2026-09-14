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
public sealed class Sst1138FreeStandingBlockCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.FreeStandingBlock.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Splice the block's statements into the enclosing block",
            nameof(Sst1138FreeStandingBlockCodeFixProvider),
            Resolve,
            Apply);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not { Parent: BlockSyntax parent } block)
        {
            return;
        }

        var index = parent.Statements.IndexOf(block);
        editor.ReplaceNode(parent, (current, _) => current is BlockSyntax currentBlock ? Splice(currentBlock, index) : current);
    }

    /// <summary>Splices a resolved free-standing block into its parent.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="block">The free-standing block, whose parent is a block.</param>
    /// <returns>The updated document.</returns>
    private static Document Apply(Document document, SyntaxNode root, BlockSyntax block)
    {
        var parent = (BlockSyntax)block.Parent!;
        return document.WithSyntaxRoot(root.ReplaceNode(parent, Splice(parent, parent.Statements.IndexOf(block))));
    }

    /// <summary>Resolves the reported free-standing block.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The block, or <see langword="null"/> when the shape no longer matches.</returns>
    /// <remarks>
    /// A directive inside the block declines the fix: splicing drops the braces, and the <c>#endif</c> or
    /// <c>#endregion</c> closing a region inside is the leading trivia of the brace that goes.
    /// </remarks>
    private static BlockSyntax? Resolve(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is BlockSyntax { Parent: BlockSyntax } block
            && !DirectiveBoundaries.Cross(block, block.FullSpan)
            ? block
            : null;

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
