// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Moves an SST1146 <c>if</c> statement to a new line.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1146ConditionalOnNewLineCodeFixProvider))]
[Shared]
public sealed class Sst1146ConditionalOnNewLineCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly TextChangeBatchFixAllProvider FixAll = new(RegisterTextChanges);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ConditionalOnNewLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TextChangeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).IsKind(SyntaxKind.IfKeyword) ? "Move 'if' to a new line" : null,
            nameof(Sst1146ConditionalOnNewLineCodeFixProvider),
            RegisterTextChanges);

    /// <summary>Adds the text changes that fix one diagnostic.</summary>
    /// <param name="text">The document's original text.</param>
    /// <param name="root">The document's original syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="changes">The text changes for the whole document.</param>
    internal static void RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        if (!token.IsKind(SyntaxKind.IfKeyword))
        {
            return;
        }

        changes.Add(BuildChange(text, token));
    }

    /// <summary>Builds the change that replaces the <c>if</c> keyword's separating whitespace with a newline plus indentation.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="token">The <c>if</c> keyword.</param>
    /// <returns>The text change to apply.</returns>
    private static TextChange BuildChange(SourceText text, SyntaxToken token)
    {
        var previous = token.GetPreviousToken();
        var line = text.Lines.GetLineFromPosition(previous.SpanStart);
        var lineText = text.ToString(line.Span);
        var lineSpan = lineText.AsSpan();
        var indentationLength = 0;
        while (indentationLength < lineSpan.Length && lineSpan[indentationLength] is ' ' or '\t')
        {
            indentationLength++;
        }

        var newLine = LayoutFixHelpers.DetectNewLine(text);
        var builder = new System.Text.StringBuilder(indentationLength + 1);
        _ = builder.Append(newLine).Append(lineText, 0, indentationLength);
        var separatingTrivia = TextSpan.FromBounds(previous.Span.End, token.SpanStart);
        return new(separatingTrivia, builder.ToString());
    }
}
