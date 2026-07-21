// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Moves a wrapped call-chain link's line break to the configured side (SST1529).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1529NullConditionalNewLineCodeFixProvider))]
[Shared]
public sealed class Sst1529NullConditionalNewLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.NullConditionalNewLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var probe = new List<TextChange>(2);
            if (!TryAppendChanges(text, root, diagnostic, probe))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Move the line break to the other side",
                    cancellationToken => MoveAsync(context.Document, diagnostic, cancellationToken),
                    equivalenceKey: nameof(Sst1529NullConditionalNewLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => TryAppendChanges(text, root, diagnostic, changes);

    /// <summary>Rewrites the gaps around a chain link so its break sits on the configured side.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> MoveAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var changes = new List<TextChange>(2);
        return TryAppendChanges(text, root, diagnostic, changes)
            ? document.WithText(text.WithChanges(changes))
            : document;
    }

    /// <summary>Appends the changes that move a wrapped chain link's break to the configured side.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The change set to append to.</param>
    /// <returns><see langword="true"/> when changes were appended.</returns>
    private static bool TryAppendChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var linkNode = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent;
        if (linkNode is null || !LayoutHelpers.TryGetChainLink(linkNode, out var leadToken, out var afterToken, out var nameToken))
        {
            return false;
        }

        var breakBefore = LayoutHelpers.HasLineBreakBefore(leadToken);
        var breakAfter = LayoutHelpers.HasLineBreakAfter(afterToken);
        if (breakBefore == breakAfter)
        {
            return false;
        }

        var wantBreakBefore = diagnostic.Properties.TryGetValue(LayoutHelpers.BreakBeforeProperty, out var value) && value == "true";
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return false;
        }

        return LayoutFixHelpers.TryAppendChainLinkBreakMove(
            text,
            leadToken,
            afterToken,
            nameToken,
            breakBefore,
            wantBreakBefore,
            changes);
    }
}
