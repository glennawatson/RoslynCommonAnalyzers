// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>&lt;summary&gt;</c> that is spread across multiple lines even
/// though its combined text is short enough to fit on a single line (SST1653).
/// The limit defaults to 100 characters and is set with
/// <c>stylesharp.summary_single_line_max_length</c> in <c>.editorconfig</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1653SingleLineSummaryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DocumentationRules.SingleLineSummary);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SingleLineDocumentationCommentTrivia);
    }

    /// <summary>Analyzes a documentation comment's summary element for an avoidable multi-line layout.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not DocumentationCommentTriviaSyntax documentation)
        {
            return;
        }

        var summary = FindSummary(documentation);
        if (summary is null)
        {
            return;
        }

        var tree = documentation.SyntaxTree;
        var lineSpan = tree.GetLineSpan(summary.Span, context.CancellationToken);
        if (lineSpan.StartLinePosition.Line == lineSpan.EndLinePosition.Line)
        {
            // Already on a single line.
            return;
        }

        var length = NormalizedTextLength(summary);
        if (length == 0)
        {
            // Empty summary — out of scope for this rule.
            return;
        }

        var maxLength = DocumentationOptions.ReadSummaryMaxLength(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        if (length >= maxLength)
        {
            return;
        }

        if (!CollapsedLineFits(context, summary, lineSpan.StartLinePosition.Character))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.SingleLineSummary, summary.GetLocation()));
    }

    /// <summary>Returns whether the collapsed summary would still sit within the line-length budget.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="summary">The summary element.</param>
    /// <param name="indentation">The column the summary's opening tag starts at.</param>
    /// <returns><see langword="true"/> when the one-line form fits, or when no budget is in force.</returns>
    /// <remarks>
    /// Collapsing a summary that then breaks the maximum-line-length rule leaves no layout that satisfies
    /// both rules, so the summary keeps its wrapped form. The length is measured through the same routine
    /// the code fix builds with, so the prediction and the rewrite cannot disagree.
    /// </remarks>
    private static bool CollapsedLineFits(in SyntaxNodeAnalysisContext context, XmlElementSyntax summary, int indentation)
    {
        var tree = summary.SyntaxTree;
        var innerSpan = TextSpan.FromBounds(summary.StartTag.Span.End, summary.EndTag.Span.Start);
        var collapsedLength = indentation
            + summary.StartTag.Span.Length
            + SummaryCollapse.CollapsedLength(tree.GetText(context.CancellationToken), innerSpan)
            + summary.EndTag.Span.Length;

        return LineLengthBudget.Fits(collapsedLength, tree, context.Options, context.Compilation, context.CancellationToken);
    }

    /// <summary>Returns the first <c>&lt;summary&gt;</c> element in a documentation comment, or <see langword="null"/>.</summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <returns>The summary element, or <see langword="null"/>.</returns>
    private static XmlElementSyntax? FindSummary(DocumentationCommentTriviaSyntax documentation)
    {
        foreach (var node in documentation.Content)
        {
            if (node is XmlElementSyntax element && element.StartTag.Name.LocalName.ValueText == "summary")
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>
    /// Counts the summary's visible text length with runs of whitespace (including
    /// the line breaks and <c>///</c> exteriors, which live in trivia) collapsed to
    /// a single space and the ends trimmed — without allocating.
    /// </summary>
    /// <param name="summary">The summary element.</param>
    /// <returns>The normalized text length.</returns>
    private static int NormalizedTextLength(XmlElementSyntax summary)
    {
        var state = default(NormalizedTextState);
        _ = DescendantTraversalHelper.VisitDescendantTokens(
            summary,
            ref state,
            static (in SyntaxToken token, ref NormalizedTextState scan) =>
            {
                if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
                {
                    if (token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken) && scan.Started)
                    {
                        scan.PendingSpace = true;
                    }

                    return true;
                }

                foreach (var character in token.ValueText)
                {
                    if (char.IsWhiteSpace(character))
                    {
                        scan.PendingSpace = scan.Started;
                        continue;
                    }

                    if (scan.PendingSpace)
                    {
                        scan.Length++;
                        scan.PendingSpace = false;
                    }

                    scan.Length++;
                    scan.Started = true;
                }

                return true;
            });

        return state.Length;
    }

    /// <summary>Tracks visible text and whitespace across adjacent XML tokens.</summary>
    private record struct NormalizedTextState
    {
        /// <summary>Gets or sets the normalized character count.</summary>
        public int Length { get; set; }

        /// <summary>Gets or sets a value indicating whether visible text has begun.</summary>
        public bool Started { get; set; }

        /// <summary>Gets or sets a value indicating whether the next visible character needs a separating space.</summary>
        public bool PendingSpace { get; set; }
    }
}
