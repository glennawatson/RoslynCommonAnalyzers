// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Inserts the configured file header at the top of a file missing it (SST1633).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1633FileHeaderCodeFixProvider))]
[Shared]
public sealed class Sst1633FileHeaderCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.FileHeader.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(FileHeaderHelper.HeaderProperty, out var header) || string.IsNullOrEmpty(header))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add file header",
                    cancellationToken => AddHeaderAsync(context.Document, header!, cancellationToken),
                    equivalenceKey: nameof(Sst1633FileHeaderCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <summary>Inserts the rendered header (re-joined with the file's newline) at the top of the file.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="header">The rendered header, lines joined by "\n".</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> AddHeaderAsync(Document document, string header, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newLine = DetectNewLine(text);
        var headerBlock = header.Replace("\n", newLine) + newLine;
        return document.WithText(text.WithChanges(new TextChange(new(0, 0), headerBlock)));
    }

    /// <summary>Detects the newline sequence used by the file (defaults to "\n").</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The newline string.</returns>
    private static string DetectNewLine(SourceText text)
    {
        if (text.Lines.Count > 0)
        {
            var first = text.Lines[0];
            var lineBreak = text.ToString(TextSpan.FromBounds(first.End, first.EndIncludingLineBreak));
            if (lineBreak.Length > 0)
            {
                return lineBreak;
            }
        }

        return "\n";
    }
}
