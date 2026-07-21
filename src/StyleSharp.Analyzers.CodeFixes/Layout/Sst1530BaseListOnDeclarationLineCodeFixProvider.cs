// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Joins a base list to the end of its type declaration line (SST1530).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1530BaseListOnDeclarationLineCodeFixProvider))]
[Shared]
public sealed class Sst1530BaseListOnDeclarationLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.BaseListOnDeclarationLine.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not BaseListSyntax)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Join the base list to the declaration",
                    cancellationToken => JoinAsync(context.Document, diagnostic.Location.SourceSpan, cancellationToken),
                    equivalenceKey: nameof(Sst1530BaseListOnDeclarationLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => AppendChange(text, root, diagnostic.Location.SourceSpan, changes);

    /// <summary>Collapses the break before the base list colon into a single space.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="span">The base list span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> JoinAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var changes = new List<TextChange>(1);
        AppendChange(text, root, span, changes);
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Appends the single change that pulls the base list up onto the declaration line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The base list span.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void AppendChange(SourceText text, SyntaxNode root, TextSpan span, List<TextChange> changes)
    {
        if (root.FindToken(span.Start).Parent is not BaseListSyntax baseList)
        {
            return;
        }

        var colon = baseList.ColonToken;
        var previousEnd = colon.GetPreviousToken().Span.End;
        if (!LayoutFixHelpers.IsWhitespaceBetween(text, previousEnd, colon.SpanStart))
        {
            return;
        }

        changes.Add(new TextChange(TextSpan.FromBounds(previousEnd, colon.SpanStart), " "));
    }
}
