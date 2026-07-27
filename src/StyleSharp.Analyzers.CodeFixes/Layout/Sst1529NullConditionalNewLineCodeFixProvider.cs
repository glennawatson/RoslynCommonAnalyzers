// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => TextChangeCodeFix.RegisterAsync(context, "Move the line break to the other side", nameof(Sst1529NullConditionalNewLineCodeFixProvider), TryAppendChanges);

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => TryAppendChanges(text, root, diagnostic, changes);

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
