// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a single-line (<c>//</c>) or multi-line (<c>/* */</c>) comment that has no text
/// content (SST1120). Empty comments add visual noise without documenting anything. Comments
/// made of extra slashes (<c>////</c>, used to comment out code) keep their content and are exempt.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommentContentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The width of the <c>//</c> or <c>/*</c> comment opener.</summary>
    private const int OpenerLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.CommentMustContainText);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports every empty comment in the tree.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var trivia in root.DescendantTrivia())
        {
            var isEmpty =
                (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && IsEmptySingleLine(text, trivia.Span))
                || (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && IsEmptyMultiLine(text, trivia.Span));
            if (isEmpty)
            {
                context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.CommentMustContainText, Location.Create(context.Tree, trivia.Span)));
            }
        }
    }

    /// <summary>Returns whether a <c>//</c> comment contains only whitespace after the opener.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The comment span.</param>
    /// <returns><see langword="true"/> when no non-whitespace character follows the '//'.</returns>
    private static bool IsEmptySingleLine(SourceText text, TextSpan span)
        => !HasContent(text, span.Start + OpenerLength, span.End);

    /// <summary>Returns whether a <c>/* */</c> comment contains only whitespace between the delimiters.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The comment span.</param>
    /// <returns><see langword="true"/> when no non-whitespace character sits between '/*' and '*/'.</returns>
    private static bool IsEmptyMultiLine(SourceText text, TextSpan span)
        => !HasContent(text, span.Start + OpenerLength, span.End - OpenerLength);

    /// <summary>Returns whether any non-whitespace character appears in the half-open range.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <returns><see langword="true"/> when a non-whitespace character is present.</returns>
    private static bool HasContent(SourceText text, int start, int end)
    {
        for (var position = start; position < end; position++)
        {
            if (!char.IsWhiteSpace(text[position]))
            {
                return true;
            }
        }

        return false;
    }
}
