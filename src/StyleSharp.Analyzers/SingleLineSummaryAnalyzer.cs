// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>&lt;summary&gt;</c> that is spread across multiple lines even
/// though its combined text is short enough to fit on a single line (SST1653).
/// The limit defaults to 100 characters and is set with
/// <c>stylesharp.summary_single_line_max_length</c> in <c>.editorconfig</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleLineSummaryAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DocumentationRules.SingleLineSummary);

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
        var lineSpan = tree.GetLineSpan(summary.Span);
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

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.SingleLineSummary, summary.GetLocation()));
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
        var length = 0;
        var started = false;
        var pendingSpace = false;

        foreach (var token in summary.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.XmlTextLiteralToken))
            {
                if (token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken) && started)
                {
                    pendingSpace = true;
                }

                continue;
            }

            foreach (var character in token.ValueText)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = started;
                    continue;
                }

                if (pendingSpace)
                {
                    length++;
                    pendingSpace = false;
                }

                length++;
                started = true;
            }
        }

        return length;
    }
}
