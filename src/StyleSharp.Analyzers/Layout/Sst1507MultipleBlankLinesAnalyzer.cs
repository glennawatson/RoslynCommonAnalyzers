// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a run of two or more consecutive blank lines (SST1507). The whole file is
/// scanned once over its line table; blank lines that fall inside a multi-line token (a
/// verbatim or raw string literal) are ignored because they are part of the literal's text.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1507MultipleBlankLinesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.MultipleBlankLines);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Scans the file's lines and reports each run of two or more blank lines.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        var root = context.Tree.GetRoot(context.CancellationToken);

        var line = 0;
        var lineCount = text.Lines.Count;
        while (line < lineCount)
        {
            if (!LayoutHelpers.IsBlankLine(text, line))
            {
                line++;
                continue;
            }

            var runStart = line;
            while (line + 1 < lineCount && LayoutHelpers.IsBlankLine(text, line + 1))
            {
                line++;
            }

            if (line > runStart && !IsInsideToken(root, text.Lines[runStart].Start))
            {
                var span = TextSpan.FromBounds(text.Lines[runStart + 1].Start, text.Lines[line].EndIncludingLineBreak);
                context.ReportDiagnostic(Diagnostic.Create(LayoutRules.MultipleBlankLines, Location.Create(context.Tree, span)));
            }

            line++;
        }
    }

    /// <summary>Returns whether the position lies inside a token's text (e.g. a multi-line string literal).</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="position">The position to probe.</param>
    /// <returns><see langword="true"/> when the position is inside token text rather than trivia.</returns>
    private static bool IsInsideToken(SyntaxNode root, int position)
    {
        var token = root.FindToken(position);
        return token.Span.Start <= position && position < token.Span.End;
    }
}
