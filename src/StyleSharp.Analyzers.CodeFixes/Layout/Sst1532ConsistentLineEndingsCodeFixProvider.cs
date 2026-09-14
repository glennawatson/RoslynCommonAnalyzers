// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites every line ending in the file to the configured newline sequence (SST1532).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1532ConsistentLineEndingsCodeFixProvider))]
[Shared]
public sealed class Sst1532ConsistentLineEndingsCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.ConsistentLineEndings.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            nameof(Sst1532ConsistentLineEndingsCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!diagnostic.Properties.TryGetValue(Sst1532ConsistentLineEndingsAnalyzer.LineEndingProperty, out var target) || target is null)
        {
            return;
        }

        AppendChanges(text, target, changes);
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
            if (!span.IsEmpty && text.ToString(span) != target)
            {
                changes.Add(new(span, target));
            }
        }
    }

    /// <summary>Words the action when the diagnostic names the line ending to normalise to.</summary>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the diagnostic names no line ending.</returns>
    private static string? TryCreateTitle(Diagnostic diagnostic) =>
        diagnostic.Properties.TryGetValue(Sst1532ConsistentLineEndingsAnalyzer.LineEndingProperty, out var target) && target is not null
            ? "Normalise the file's line endings"
            : null;
}
