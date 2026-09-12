// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Wraps the unbraced child statement of a control-flow statement in braces (SST1503, SST1519). Both
/// rules report the same defect from a different angle — one that the child has no braces at all, the
/// other that an unbraced child spans several lines — and the repair is the same statement either way.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmbeddedStatementBraceCodeFixProvider))]
[Shared]
public sealed class EmbeddedStatementBraceCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.BracesRequired.Id,
        LayoutRules.BracesForMultiLineChild.Id);

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
            if (!TryGetUnbracedChild(root, diagnostic, out var child))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add braces",
                    cancellationToken => WrapAsync(context.Document, child, cancellationToken),
                    equivalenceKey: nameof(EmbeddedStatementBraceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryGetUnbracedChild(root, diagnostic, out var child))
        {
            return;
        }

        LayoutFixHelpers.AppendBraceWrap(text, child, LayoutFixHelpers.DetectNewLine(text), changes);
    }

    /// <summary>Wraps the embedded statement in braces.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="statement">The embedded statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> WrapAsync(Document document, StatementSyntax statement, CancellationToken cancellationToken)
    {
        // Wrapping a statement inserts the opening brace before it and the closing brace after it.
        const int BraceWrapChangeCapacity = 2;

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>(BraceWrapChangeCapacity);
        LayoutFixHelpers.AppendBraceWrap(text, statement, LayoutFixHelpers.DetectNewLine(text), changes);
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Resolves the reported control-flow statement's child when it still lacks braces.</summary>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="child">The unbraced child statement.</param>
    /// <returns><see langword="true"/> when the child is present and unbraced.</returns>
    private static bool TryGetUnbracedChild(SyntaxNode root, Diagnostic diagnostic, out StatementSyntax child)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is { } control
            && LayoutHelpers.TryGetEmbeddedStatement(control, out var embedded)
            && embedded is not BlockSyntax)
        {
            child = embedded;
            return true;
        }

        child = null!;
        return false;
    }
}
