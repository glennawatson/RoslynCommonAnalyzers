// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Reports source files that consist of commented-out C# code (SST1108).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1108EntireFileCommentedOutAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The length of a single-line comment marker.</summary>
    private const int SingleLineCommentMarkerLength = 2;

    /// <summary>The length of the delimiters around a block comment.</summary>
    private const int MultiLineCommentDelimiterLength = 2;

    /// <summary>The total length of both block-comment delimiters.</summary>
    private const int MultiLineCommentMarkersLength = 4;

    /// <summary>Keywords that identify the beginning of a source declaration.</summary>
    private static readonly string[] CodeKeywords =
    [
        "namespace",
        "class",
        "struct",
        "record",
        "interface",
        "enum",
        "delegate",
        "public",
        "private",
        "protected",
        "internal",
        "static",
        "sealed",
        "abstract",
        "partial",
        "readonly",
        "file",
        "using",
    ];

    /// <summary>The descriptor this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(ReadabilityRules.EntireFileCommentedOut);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports a file whose comments reconstruct a complete C# source shape.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        if (!root.GetFirstToken(includeZeroWidth: true).IsKind(SyntaxKind.EndOfFileToken)
            || root.ContainsDirectives)
        {
            return;
        }

        var text = context.Tree.GetText(context.CancellationToken);
        if (!TryBuildCandidate(text, root.GetLeadingTrivia(), out var candidate, out var firstCodeComment)
            || candidate is null)
        {
            return;
        }

        if (!LooksLikeCompleteSource(candidate.ToString(), (CSharpParseOptions)context.Tree.Options, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ReadabilityRules.EntireFileCommentedOut,
            firstCodeComment.GetLocation()));
    }

    /// <summary>Reconstructs source from comments after skipping the file's prose and license header.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="comments">The file's leading trivia.</param>
    /// <param name="candidate">The reconstructed source.</param>
    /// <param name="firstCodeComment">The first comment that began the reconstruction.</param>
    /// <returns><see langword="true"/> when a code-shaped comment was found.</returns>
    private static bool TryBuildCandidate(
        SourceText text,
        in SyntaxTriviaList comments,
        out StringBuilder? candidate,
        out SyntaxTrivia firstCodeComment)
    {
        candidate = null;
        firstCodeComment = default;
        for (var i = 0; i < comments.Count; i++)
        {
            var comment = comments[i];
            if (!TryGetCommentBounds(text, comment, out var start, out var end))
            {
                continue;
            }

            if (candidate is null)
            {
                if (!LooksLikeCodeComment(text, start, end))
                {
                    continue;
                }

                candidate = new(text.Length - comment.SpanStart);
                firstCodeComment = comment;
            }

            AppendSourceText(candidate, text, start, end);
            _ = candidate.AppendLine();
        }

        return candidate is not null;
    }

    /// <summary>Appends a source-text span without materializing an intermediate string.</summary>
    /// <param name="builder">The reconstructed source builder.</param>
    /// <param name="text">The file text.</param>
    /// <param name="start">The content start.</param>
    /// <param name="end">The content end.</param>
    private static void AppendSourceText(StringBuilder builder, SourceText text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            _ = builder.Append(text[i]);
        }
    }

    /// <summary>Returns the source span inside a comment, excluding its marker or delimiters.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="comment">The trivia to inspect.</param>
    /// <param name="start">The content start.</param>
    /// <param name="end">The content end.</param>
    /// <returns><see langword="true"/> for a regular source comment.</returns>
    private static bool TryGetCommentBounds(SourceText text, in SyntaxTrivia comment, out int start, out int end)
    {
        var span = comment.Span;
        if (comment.IsKind(SyntaxKind.SingleLineCommentTrivia))
        {
            if (IsDocumentationComment(text, span))
            {
                start = 0;
                end = 0;
                return false;
            }

            start = span.Start + SingleLineCommentMarkerLength;
            end = span.End;
            return true;
        }

        if (comment.IsKind(SyntaxKind.MultiLineCommentTrivia) && span.Length > MultiLineCommentMarkersLength)
        {
            start = span.Start + MultiLineCommentDelimiterLength;
            end = span.End - MultiLineCommentDelimiterLength;
            return true;
        }

        start = 0;
        end = 0;
        return false;
    }

    /// <summary>Returns whether a single-line comment is documentation rather than source text.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="span">The comment span.</param>
    /// <returns><see langword="true"/> for a <c>///</c> comment.</returns>
    private static bool IsDocumentationComment(SourceText text, TextSpan span) =>
        span.Length > SingleLineCommentMarkerLength && text[span.Start + SingleLineCommentMarkerLength] == '/';

    /// <summary>Returns whether a comment begins with a likely C# declaration or statement.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="start">The content start.</param>
    /// <param name="end">The content end.</param>
    /// <returns><see langword="true"/> for a code-shaped comment.</returns>
    private static bool LooksLikeCodeComment(SourceText text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        if (start >= end)
        {
            return false;
        }

        for (var i = 0; i < CodeKeywords.Length; i++)
        {
            if (StartsWithWord(text, start, end, CodeKeywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the candidate parses as a declaration-bearing C# source file.</summary>
    /// <param name="candidate">The reconstructed source.</param>
    /// <param name="parseOptions">The original source file's C# parse options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> for a complete declaration-bearing source shape.</returns>
    private static bool LooksLikeCompleteSource(string candidate, CSharpParseOptions parseOptions, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(candidate, parseOptions, cancellationToken: cancellationToken);
        var root = (CompilationUnitSyntax)tree.GetRoot(cancellationToken);
        if (root.ContainsDiagnostics)
        {
            return false;
        }

        for (var i = 0; i < root.Members.Count; i++)
        {
            if (root.Members[i] is BaseNamespaceDeclarationSyntax
                or BaseTypeDeclarationSyntax
                or DelegateDeclarationSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a source segment starts with a keyword boundary.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="start">The segment start.</param>
    /// <param name="end">The segment end.</param>
    /// <param name="keyword">The expected keyword.</param>
    /// <returns><see langword="true"/> when the keyword is followed by source whitespace or an opening brace.</returns>
    private static bool StartsWithWord(SourceText text, int start, int end, string keyword)
    {
        var keywordLength = keyword.Length;
        return StartsWith(text, start, end, keyword.AsSpan())
            && (start + keywordLength == end || char.IsWhiteSpace(text[start + keywordLength]) || text[start + keywordLength] == '{');
    }

    /// <summary>Compares a source-text segment with a prefix without allocating.</summary>
    /// <param name="text">The file text.</param>
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
}
