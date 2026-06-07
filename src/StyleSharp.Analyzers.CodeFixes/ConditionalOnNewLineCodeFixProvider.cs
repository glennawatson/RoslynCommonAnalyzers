// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Moves an SST1146 <c>if</c> statement to a new line.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConditionalOnNewLineCodeFixProvider))]
[Shared]
public sealed class ConditionalOnNewLineCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ConditionalOnNewLine.Id);

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
                    equivalenceKey: nameof(ConditionalOnNewLineCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the <c>if</c> keyword's separating whitespace with a newline.</summary>
    /// <param name="document">The document to update.</param>
    /// <param name="root">The current syntax root.</param>
    /// <param name="token">The <c>if</c> keyword.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> MoveAsync(Document document, SyntaxNode root, SyntaxToken token, CancellationToken cancellationToken)
    {
        var text = await root.SyntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var previous = token.GetPreviousToken();
        var line = text.Lines.GetLineFromPosition(previous.SpanStart);
        var lineText = text.ToString(line.Span);
        var lineSpan = lineText.AsSpan();
        var indentationLength = 0;
        while (indentationLength < lineSpan.Length && lineSpan[indentationLength] is ' ' or '\t')
        {
            indentationLength++;
        }

        var builder = new System.Text.StringBuilder(indentationLength + 1);
        _ = builder.Append('\n').Append(lineText, 0, indentationLength);
        var separatingTrivia = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(previous.Span.End, token.SpanStart);
        return document.WithText(text.WithChanges(new Microsoft.CodeAnalysis.Text.TextChange(separatingTrivia, builder.ToString())));
    }
}
