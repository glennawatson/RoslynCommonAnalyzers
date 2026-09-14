// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Converts a summary-like <c>//</c> comment into a <c>/// &lt;summary&gt;</c> documentation comment (SST1663),
/// XML-escaping the text so the result is a well-formed documentation comment.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1663SummaryCommentCodeFixProvider))]
[Shared]
public sealed class Sst1663SummaryCommentCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(DocumentationRules.SummaryComment.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync<TextChange>(
            context,
            "Convert to a '/// <summary>' documentation comment",
            nameof(Sst1663SummaryCommentCodeFixProvider),
            TryBuildChange,
            DocumentationElementFix.ApplyAsync);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (!TryBuildChange(root, diagnostic, out var change))
        {
            return;
        }

        changes.Add(change);
    }

    /// <summary>Builds the change that rewrites the <c>//</c> comment as a <c>/// &lt;summary&gt;</c> line.</summary>
    /// <param name="root">The document's syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="change">The rewrite change when one is produced.</param>
    /// <returns><see langword="true"/> when a change was built.</returns>
    private static bool TryBuildChange(SyntaxNode root, Diagnostic diagnostic, out TextChange change)
    {
        const int SingleLineCommentMarkerLength = 2;

        change = default;

        var trivia = root.FindTrivia(diagnostic.Location.SourceSpan.Start);
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
        {
            return false;
        }

        var raw = trivia.ToString();
        var content = XmlTextEscaping.Escape(raw.AsSpan(SingleLineCommentMarkerLength).Trim());
        change = new(trivia.Span, $"/// <summary>{content}</summary>");
        return true;
    }
}
