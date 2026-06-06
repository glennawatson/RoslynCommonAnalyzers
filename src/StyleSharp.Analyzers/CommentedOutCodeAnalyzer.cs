// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Reports regular comments that conservatively resemble commented-out C# statements (SST1148).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommentedOutCodeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The minimum content length considered code-like.</summary>
    private const int MinimumCodeLength = 4;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.NoCommentedOutCode);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Scans regular comment trivia after the file header.</summary>
    /// <param name="context">The syntax-tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var firstTokenStart = root.GetFirstToken().SpanStart;
        var text = context.Tree.GetText(context.CancellationToken);
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.SpanStart < firstTokenStart
                || !trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || !LooksLikeCode(text, trivia.Span))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoCommentedOutCode, trivia.GetLocation()));
        }
    }

    /// <summary>Classifies a comment with span-only textual checks.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The comment span.</param>
    /// <returns><see langword="true"/> for a conservative code-like shape.</returns>
    private static bool LooksLikeCode(SourceText text, TextSpan span)
    {
        var start = span.Start + 2;
        var end = span.End;
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        if (end - start < MinimumCodeLength || StartsWithMarker(text, start, end))
        {
            return false;
        }

        return IsCodeTerminator(text[end - 1]) || StartsWithCodeKeyword(text, start, end);
    }

    /// <summary>Returns whether text starts with a non-code comment marker.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The content start.</param>
    /// <param name="end">The content end.</param>
    /// <returns><see langword="true"/> when a marker is present.</returns>
    private static bool StartsWithMarker(SourceText text, int start, int end)
        => StartsWith(text, start, end, "TODO".AsSpan())
            || StartsWith(text, start, end, "HACK".AsSpan())
            || StartsWith(text, start, end, "http".AsSpan())
            || text[start] is '-' or '=' or '*';

    /// <summary>Compares a source-text segment with a span without allocating.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The segment start.</param>
    /// <param name="end">The segment end.</param>
    /// <param name="value">The expected prefix.</param>
    /// <returns><see langword="true"/> when the segment starts with the prefix.</returns>
    private static bool StartsWith(SourceText text, int start, int end, ReadOnlySpan<char> value)
    {
        if (end - start < value.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (text[start + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a character strongly indicates a C# statement.</summary>
    /// <param name="character">The final content character.</param>
    /// <returns><see langword="true"/> for a statement or block terminator.</returns>
    private static bool IsCodeTerminator(char character) => character is ';' or '{' or '}';

    /// <summary>Returns whether content starts with a statement-oriented C# keyword.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The content start.</param>
    /// <param name="end">The content end.</param>
    /// <returns><see langword="true"/> when a recognized keyword is present.</returns>
    private static bool StartsWithCodeKeyword(SourceText text, int start, int end)
        => StartsWith(text, start, end, "return ".AsSpan())
            || StartsWith(text, start, end, "throw ".AsSpan())
            || StartsWith(text, start, end, "var ".AsSpan())
            || StartsWithControlKeyword(text, start, end);

    /// <summary>Returns whether content starts with a control-flow keyword.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The content start.</param>
    /// <param name="end">The content end.</param>
    /// <returns><see langword="true"/> when a recognized keyword is present.</returns>
    private static bool StartsWithControlKeyword(SourceText text, int start, int end)
        => StartsWith(text, start, end, "if (".AsSpan())
            || StartsWith(text, start, end, "for (".AsSpan())
            || StartsWith(text, start, end, "while (".AsSpan());
}
