// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Expands a single-line statement block (SST1501) or single-line element body (SST1502)
/// onto multiple lines, placing each statement and the closing brace on its own line with
/// the correct indentation. Type and enum bodies (whose members are not plain statements)
/// have no fix offered; the rewrite is skipped when a comment shares a line with the code.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SingleLineBlockReflowCodeFixProvider))]
[Shared]
public sealed class SingleLineBlockReflowCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.StatementOnOwnLine.Id,
        LayoutRules.ElementOnOwnLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not BlockSyntax { Statements.Count: > 0 } block)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Expand onto multiple lines",
                    cancellationToken => ReflowAsync(context.Document, block, cancellationToken),
                    equivalenceKey: nameof(SingleLineBlockReflowCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not BlockSyntax { Statements.Count: > 0 } block)
        {
            return;
        }

        var newLine = LayoutFixHelpers.DetectNewLine(text);
        LayoutFixHelpers.TryAppendBlockExpansion(text, block, newLine, changes);
    }

    /// <summary>Builds the line breaks that spread the block's statements and closing brace across lines.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="block">The single-line block to expand.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, unchanged when a comment blocks the rewrite.</returns>
    internal static async Task<Document> ReflowAsync(Document document, BlockSyntax block, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var changes = new List<TextChange>(block.Statements.Count + 1);

        return LayoutFixHelpers.TryAppendBlockExpansion(text, block, newLine, changes)
            ? document.WithText(text.WithChanges(changes))
            : document;
    }
}
