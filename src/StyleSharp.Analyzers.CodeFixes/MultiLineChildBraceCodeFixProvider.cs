// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Wraps a multi-line embedded statement in braces (SST1519).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MultiLineChildBraceCodeFixProvider))]
[Shared]
public sealed class MultiLineChildBraceCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.BracesForMultiLineChild.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not { } control
                || !LayoutHelpers.TryGetEmbeddedStatement(control, out var child)
                || child is BlockSyntax)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add braces",
                    cancellationToken => WrapAsync(context.Document, child, cancellationToken),
                    equivalenceKey: nameof(MultiLineChildBraceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Wraps the embedded statement in braces.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="statement">The embedded statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> WrapAsync(Document document, StatementSyntax statement, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>(2);
        LayoutFixHelpers.AppendBraceWrap(text, statement, LayoutFixHelpers.DetectNewLine(text), changes);
        return document.WithText(text.WithChanges(changes));
    }
}
