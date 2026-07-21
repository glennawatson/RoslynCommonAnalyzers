// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Collapses a multi-line object or collection initializer onto a single line (SST1531).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1531InitializerOnSingleLineCodeFixProvider))]
[Shared]
public sealed class Sst1531InitializerOnSingleLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.InitializerOnSingleLine.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not InitializerExpressionSyntax)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Collapse onto a single line",
                    cancellationToken => CollapseAsync(context.Document, diagnostic.Location.SourceSpan, cancellationToken),
                    equivalenceKey: nameof(Sst1531InitializerOnSingleLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not InitializerExpressionSyntax initializer)
        {
            return;
        }

        AppendCollapse(text, initializer, changes);
    }

    /// <summary>Collapses the initializer at the diagnostic span onto one line.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="span">The initializer's opening-brace span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> CollapseAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || root.FindToken(span.Start).Parent is not InitializerExpressionSyntax initializer)
        {
            return document;
        }

        var changes = new List<TextChange>(initializer.Expressions.Count + 2);
        AppendCollapse(text, initializer, changes);
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Appends the changes that collapse each wrapped gap of the initializer to its canonical spacing.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="initializer">The initializer to collapse.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void AppendCollapse(SourceText text, InitializerExpressionSyntax initializer, List<TextChange> changes)
    {
        var close = initializer.CloseBraceToken;
        var pending = new List<TextChange>(initializer.Expressions.Count + 2);
        var token = initializer.OpenBraceToken.GetPreviousToken();
        while (!token.IsKind(SyntaxKind.None))
        {
            var next = token.GetNextToken();
            LayoutHelpers.ClassifyGap(text, token.Span.End, next.SpanStart, out var hasLineBreak, out var isClean);
            if (hasLineBreak && !isClean)
            {
                return;
            }

            if (hasLineBreak)
            {
                pending.Add(new TextChange(TextSpan.FromBounds(token.Span.End, next.SpanStart), next.IsKind(SyntaxKind.CommaToken) ? string.Empty : " "));
            }

            if (next.Equals(close))
            {
                break;
            }

            token = next;
        }

        changes.AddRange(pending);
    }
}
