// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Inserts a blank line before a member that is not separated from the previous one (SST1516).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1516ElementSpacingCodeFixProvider))]
[Shared]
public sealed class Sst1516ElementSpacingCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(LayoutRules.ElementsSeparatedByBlankLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Insert blank line",
            nameof(Sst1516ElementSpacingCodeFixProvider),
            static (root, diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>(),
            InsertBlankLineAsync);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
        {
            return;
        }

        changes.Add(BuildChange(text, member));
    }

    /// <summary>Inserts a blank line directly above the member's documentation header or first token.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="member">The member to separate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> InsertBlankLineAsync(Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(BuildChange(text, member)));
    }

    /// <summary>Builds the blank-line insertion above the member.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="member">The member to separate.</param>
    /// <returns>The text change that inserts the blank line.</returns>
    private static TextChange BuildChange(SourceText text, MemberDeclarationSyntax member)
    {
        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var contentStartLine = LayoutHelpers.ContentStartLine(text, member);
        var position = text.Lines[contentStartLine].Start;
        return new(new(position, 0), newLine);
    }
}
