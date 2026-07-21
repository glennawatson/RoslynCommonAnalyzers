// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites every line ending in the file to the configured newline sequence (SST1532).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1532ConsistentLineEndingsCodeFixProvider))]
[Shared]
public sealed class Sst1532ConsistentLineEndingsCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.ConsistentLineEndings.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(Sst1532ConsistentLineEndingsAnalyzer.LineEndingProperty, out var target) || target is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Normalise the file's line endings",
                    cancellationToken => NormaliseAsync(context.Document, target, cancellationToken),
                    equivalenceKey: nameof(Sst1532ConsistentLineEndingsCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!diagnostic.Properties.TryGetValue(Sst1532ConsistentLineEndingsAnalyzer.LineEndingProperty, out var target) || target is null)
        {
            return;
        }

        AppendChanges(text, target, changes);
    }

    /// <summary>Rewrites every non-conforming line ending to the target newline.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="target">The required newline sequence.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> NormaliseAsync(Document document, string target, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<TextChange>(text.Lines.Count);
        AppendChanges(text, target, changes);
        return changes.Count == 0 ? document : document.WithText(text.WithChanges(changes));
    }

    /// <summary>Appends a replacement for each line ending that differs from the target newline.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="target">The required newline sequence.</param>
    /// <param name="changes">The change set to append to.</param>
    private static void AppendChanges(SourceText text, string target, List<TextChange> changes)
    {
        var lines = text.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var span = TextSpan.FromBounds(line.End, line.EndIncludingLineBreak);
            if (span.Length != 0 && text.ToString(span) != target)
            {
                changes.Add(new TextChange(span, target));
            }
        }
    }
}
