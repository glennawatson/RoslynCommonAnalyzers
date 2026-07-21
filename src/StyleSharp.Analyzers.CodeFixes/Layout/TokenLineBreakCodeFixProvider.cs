// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves the single line break to the configured side of a wrapped operator token: a binary operator
/// (SST1526), an expression-body <c>=&gt;</c> (SST1527), or a wrapped initializer <c>=</c> (SST1528).
/// The target side rides on the diagnostic's <see cref="LayoutHelpers.BreakBeforeProperty"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TokenLineBreakCodeFixProvider))]
[Shared]
public sealed class TokenLineBreakCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.BinaryOperatorNewLine.Id,
        LayoutRules.ArrowTokenNewLine.Id,
        LayoutRules.EqualsTokenNewLine.Id);

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
                    equivalenceKey: nameof(TokenLineBreakCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => TryAppendChanges(text, root, diagnostic, changes);

    /// <summary>Rewrites the two gaps around the token so its break sits on the configured side.</summary>
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

    /// <summary>Appends the break-moving changes when the token carries exactly one break on the wrong side.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The change set to append to.</param>
    /// <returns><see langword="true"/> when changes were appended.</returns>
    private static bool TryAppendChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        var breakBefore = LayoutHelpers.HasLineBreakBefore(token);
        var breakAfter = LayoutHelpers.HasLineBreakAfter(token);
        if (breakBefore == breakAfter)
        {
            return false;
        }

        var wantBreakBefore = diagnostic.Properties.TryGetValue(LayoutHelpers.BreakBeforeProperty, out var value) && value == "true";
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return false;
        }

        return LayoutFixHelpers.TryAppendTokenBreakMove(text, token, breakBefore, wantBreakBefore, LayoutFixHelpers.DetectNewLine(text), changes);
    }
}
