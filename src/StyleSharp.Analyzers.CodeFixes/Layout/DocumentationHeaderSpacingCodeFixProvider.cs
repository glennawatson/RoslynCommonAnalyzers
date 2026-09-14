// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Fixes documentation-header spacing: inserts a blank line before a header that lacks one
/// (SST1514) and removes the blank line between a header and its element (SST1506).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationHeaderSpacingCodeFixProvider))]
[Shared]
public sealed class DocumentationHeaderSpacingCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.DocHeaderPrecededByBlankLine.Id,
        LayoutRules.DocHeaderNotFollowedByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            nameof(DocumentationHeaderSpacingCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member
            || !LayoutHelpers.TryGetDocHeader(member, out _))
        {
            return;
        }

        var insertBefore = diagnostic.Id == LayoutRules.DocHeaderPrecededByBlankLine.Id;
        changes.Add(BuildChange(text, member, insertBefore));
    }

    /// <summary>Builds the change that inserts or removes the blank line around the member's documentation header.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="member">The documented member.</param>
    /// <param name="insertBefore">When <see langword="true"/>, inserts a blank line before the header; otherwise removes the blank line after it.</param>
    /// <returns>The text change to apply.</returns>
    private static TextChange BuildChange(SourceText text, MemberDeclarationSyntax member, bool insertBefore)
    {
        _ = LayoutHelpers.TryGetDocHeader(member, out var header);

        if (insertBefore)
        {
            var headerFirstLine = LayoutHelpers.LineOf(text, header.SpanStart);
            var position = text.Lines[headerFirstLine].Start;
            return new(new(position, 0), LayoutFixHelpers.DetectNewLine(text));
        }

        var headerLastLine = LayoutHelpers.LineOf(text, header.Span.End - 1);
        var memberLine = LayoutHelpers.StartLine(text, member.GetFirstToken());
        var span = TextSpan.FromBounds(text.Lines[headerLastLine + 1].Start, text.Lines[memberLine].Start);
        return new(span, string.Empty);
    }

    /// <summary>Words the action for a documented member whose header spacing the diagnostic reports.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the member no longer carries a documentation header.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is { } member && LayoutHelpers.TryGetDocHeader(member, out _)
            ? TitleFor(diagnostic.Id)
            : null;

    /// <summary>Words the action for the reported spacing rule.</summary>
    /// <param name="diagnosticId">The reported rule id.</param>
    /// <returns>The code action title.</returns>
    private static string TitleFor(string diagnosticId) =>
        diagnosticId == LayoutRules.DocHeaderPrecededByBlankLine.Id ? "Insert blank line before documentation" : "Remove blank line after documentation";
}
