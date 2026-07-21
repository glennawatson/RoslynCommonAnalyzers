// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>&lt;summary&gt;</c> whose prose is split into paragraphs by blank documentation lines but
/// which has no <c>&lt;para&gt;</c> elements to mark them (SST1664). The documentation pipeline collapses the
/// blank lines, so the paragraphs render as one run. Off by default.
/// </summary>
/// <remarks>
/// To keep the fix safe the rule only fires on a canonical, pure-prose summary: the <c>&lt;summary&gt;</c> and
/// <c>&lt;/summary&gt;</c> tags each sit alone on their line, the content in between is plain text with no
/// nested elements, and at least one blank documentation line separates prose from prose.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1664SummaryParagraphAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DocumentationRules.SummaryParagraph);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.XmlElement);
    }

    /// <summary>Reports a pure-prose summary whose paragraphs are separated by blank lines rather than <c>&lt;para&gt;</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var element = (XmlElementSyntax)context.Node;
        if (element.StartTag.Name.LocalName.ValueText != "summary" || !IsPureProse(element))
        {
            return;
        }

        var text = element.SyntaxTree.GetText(context.CancellationToken);
        if (!SummaryParagraphLayout.TryGetInnerLineRange(text, element, out var firstInnerLine, out var lastInnerLine))
        {
            return;
        }

        if (!SummaryParagraphLayout.HasBlankSeparatedParagraphs(text, firstInnerLine, lastInnerLine))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(DocumentationRules.SummaryParagraph, element.SyntaxTree, element.Span));
    }

    /// <summary>Returns whether a summary's content is plain text with no nested XML elements.</summary>
    /// <param name="element">The summary element.</param>
    /// <returns><see langword="true"/> when the content is pure prose.</returns>
    private static bool IsPureProse(XmlElementSyntax element)
    {
        foreach (var node in element.Content)
        {
            if (node is XmlElementSyntax or XmlEmptyElementSyntax)
            {
                return false;
            }
        }

        return true;
    }
}
