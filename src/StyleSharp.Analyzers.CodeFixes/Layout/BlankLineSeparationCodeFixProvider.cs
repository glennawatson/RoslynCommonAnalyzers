// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a blank line left inside a construct that should read as one thing (SST1535, SST1536, SST1537).
/// </summary>
/// <remarks>
/// The edit is pure whitespace, so the fix works on the source text rather than the tree: it never has to
/// rebuild a node, and the surrounding trivia is left exactly as written.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlankLineSeparationCodeFixProvider))]
[Shared]
public sealed class BlankLineSeparationCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        LayoutRules.BlankLineAfterConstructorInitializerColon.Id,
        LayoutRules.BlankLineAfterConditionalToken.Id,
        LayoutRules.BlankLineAfterArrow.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => TextChangeCodeFix.RegisterAsync(context, "Remove the blank line", nameof(BlankLineSeparationCodeFixProvider), TryAppendChanges);

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => TryAppendChanges(text, root, diagnostic, changes);

    /// <summary>Builds the whitespace edit one diagnostic asks for.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="changes">The list the edit is appended to.</param>
    /// <returns><see langword="true"/> when an edit was appended.</returns>
    private static bool TryAppendChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
        => TryAppendDeletion(text, diagnostic, changes);

    /// <summary>Deletes the blank lines between the reported token and whatever follows it.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="diagnostic">The diagnostic reported on the token.</param>
    /// <param name="changes">The list the edit is appended to.</param>
    /// <returns><see langword="true"/> when an edit was appended.</returns>
    private static bool TryAppendDeletion(SourceText text, Diagnostic diagnostic, List<TextChange> changes)
    {
        var tokenLine = text.Lines.GetLineFromPosition(diagnostic.Location.SourceSpan.Start).LineNumber;
        var firstBlank = tokenLine + 1;
        var lastBlank = firstBlank;
        while (lastBlank + 1 < text.Lines.Count && IsBlank(text, lastBlank + 1))
        {
            lastBlank++;
        }

        if (!IsBlank(text, firstBlank))
        {
            return false;
        }

        changes.Add(new TextChange(
            TextSpan.FromBounds(text.Lines[firstBlank].Start, text.Lines[lastBlank].EndIncludingLineBreak),
            string.Empty));
        return true;
    }

    /// <summary>Returns whether a line holds nothing but whitespace.</summary>
    /// <param name="text">The document's source text.</param>
    /// <param name="lineIndex">The zero-based line index.</param>
    /// <returns><see langword="true"/> when the line is blank.</returns>
    private static bool IsBlank(SourceText text, int lineIndex)
    {
        var line = text.Lines[lineIndex];
        for (var i = line.Start; i < line.End; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
