// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Moves an SST1146 <c>if</c> statement to a new line.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1146ConditionalOnNewLineCodeFixProvider))]
[Shared]
public sealed class Sst1146ConditionalOnNewLineCodeFixProvider : CodeFixProvider, ITextChangeBatchableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ConditionalOnNewLine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => TextChangeBatchFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        for (var i = 0; i < context.Diagnostics.Length; i++)
        {
            var diagnostic = context.Diagnostics[i];
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (!token.IsKind(SyntaxKind.IfKeyword))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Move 'if' to a new line",
                    cancellationToken => MoveAsync(context.Document, root, token, cancellationToken),
                    equivalenceKey: nameof(Sst1146ConditionalOnNewLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void ITextChangeBatchableCodeFix.RegisterTextChanges(SourceText text, SyntaxNode root, Diagnostic diagnostic, List<TextChange> changes)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        if (!token.IsKind(SyntaxKind.IfKeyword))
        {
            return;
        }

        changes.Add(BuildChange(text, token));
    }

    /// <summary>Replaces the <c>if</c> keyword's separating whitespace with a newline.</summary>
    /// <param name="document">The document to update.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="token">The <c>if</c> keyword.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    internal static async Task<Document> MoveAsync(Document document, SyntaxNode root, SyntaxToken token, CancellationToken cancellationToken)
    {
        var text = await root.SyntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(BuildChange(text, token)));
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
        return new TextChange(separatingTrivia, builder.ToString());
    }
}
