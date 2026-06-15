// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Normalises the end of the file to a single trailing newline (SST1518).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1518FileEndingCodeFixProvider))]
[Shared]
public sealed class Sst1518FileEndingCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.LineEndingsAtEndOfFile.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    "End the file with a single newline",
                    cancellationToken => NormaliseAsync(context.Document, diagnostic.Location.SourceSpan, cancellationToken),
                    equivalenceKey: nameof(Sst1518FileEndingCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => changes.Add(new TextChange(diagnostic.Location.SourceSpan, LayoutFixHelpers.DetectNewLine(text)));

    /// <summary>Replaces the trailing whitespace span with a single newline.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="span">The trailing whitespace span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> NormaliseAsync(Document document, TextSpan span, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(new TextChange(span, LayoutFixHelpers.DetectNewLine(text))));
    }
}
