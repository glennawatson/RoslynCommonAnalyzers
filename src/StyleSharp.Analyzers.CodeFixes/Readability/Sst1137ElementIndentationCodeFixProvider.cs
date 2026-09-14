// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shifts a reported element onto the indentation its siblings use (SST1137). Every line the element spans
/// moves by the same width, so nested code keeps its relative shape.
/// </summary>
/// <remarks>
/// An element holding a verbatim or raw string literal is left alone: those carry their own line breaks and
/// indentation as content, so shifting the lines would edit the string's value.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1137ElementIndentationCodeFixProvider))]
[Shared]
public sealed class Sst1137ElementIndentationCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ElementsConsistentIndentation.Id);

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
            if (!diagnostic.Properties.TryGetValue(Sst1137ElementIndentationAnalyzer.ReferenceIndentProperty, out var text)
                || !int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var reference)
                || Element(root, diagnostic.Location.SourceSpan) is not { } element
                || CarriesItsOwnLineBreaks(element))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Match the siblings' indentation",
                    cancellationToken => ShiftAsync(context.Document, element, reference, cancellationToken),
                    equivalenceKey: nameof(Sst1137ElementIndentationCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Gets the declaration or statement the diagnostic's first token belongs to.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The reported token's span.</param>
    /// <returns>The element, or <see langword="null"/> when the shape no longer matches.</returns>
    private static SyntaxNode? Element(SyntaxNode root, TextSpan span)
    {
        for (var node = root.FindToken(span.Start).Parent; node is not null; node = node.Parent)
        {
            if (node is MemberDeclarationSyntax or StatementSyntax)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Returns whether an element holds a literal whose own text spans lines.</summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when shifting the lines would edit a string's value.</returns>
    private static bool CarriesItsOwnLineBreaks(SyntaxNode element)
    {
        var found = false;
        _ = DescendantTraversalHelper.VisitDescendantTokens(
            element,
            ref found,
            static (in SyntaxToken token, ref bool state) =>
            {
                state = HoldsText(token) && token.Text.IndexOf('\n') >= 0;
                return !state;
            });

        return found;
    }

    /// <summary>Returns whether a token's own text is string content.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for a string literal or interpolated-string text token.</returns>
    private static bool HoldsText(SyntaxToken token) => token.Kind() is SyntaxKind.StringLiteralToken
        or SyntaxKind.MultiLineRawStringLiteralToken
        or SyntaxKind.SingleLineRawStringLiteralToken
        or SyntaxKind.InterpolatedStringTextToken;

    /// <summary>Moves every line the element spans by the difference between its indentation and its siblings'.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="element">The element to shift.</param>
    /// <param name="reference">The indentation width the siblings use.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ShiftAsync(Document document, SyntaxNode element, int reference, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var lines = text.Lines;
        var first = lines.GetLineFromPosition(element.SpanStart);
        var delta = reference - (element.SpanStart - first.Start);
        if (delta == 0)
        {
            return document;
        }

        var last = lines.GetLineFromPosition(element.Span.End).LineNumber;
        var changes = new List<TextChange>(last - first.LineNumber + 1);
        for (var number = first.LineNumber; number <= last; number++)
        {
            var line = lines[number];
            var indent = LeadingBlankLength(text, line);
            if (indent == line.Span.Length && number != first.LineNumber)
            {
                continue;
            }

            if (delta > 0)
            {
                changes.Add(new(new TextSpan(line.Start, 0), new string(' ', delta)));
                continue;
            }

            var removable = Math.Min(-delta, indent);
            if (removable > 0)
            {
                changes.Add(new(new TextSpan(line.Start, removable), string.Empty));
            }
        }

        return document.WithText(text.WithChanges(changes));
    }

    /// <summary>Counts the whitespace a line opens with.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="line">The line.</param>
    /// <returns>The number of leading whitespace characters.</returns>
    private static int LeadingBlankLength(SourceText text, TextLine line)
    {
        var count = 0;
        for (var position = line.Start; position < line.End && char.IsWhiteSpace(text[position]); position++)
        {
            count++;
        }

        return count;
    }
}
