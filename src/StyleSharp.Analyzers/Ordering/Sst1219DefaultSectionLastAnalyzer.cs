// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>switch</c> statement whose <c>default</c> section is not the last one (SST1219). A reader
/// expects the fall-through case at the bottom, and moving it there is always safe: the language forbids
/// implicit fall-through between sections, and <c>goto case</c> / <c>goto default</c> target a label rather
/// than a position.
/// </summary>
/// <remarks>
/// Pure syntax: an indexed scan of the sections up to the second-to-last, with no semantic model. A switch
/// with a trailing <c>default</c>, or none at all, is rejected before any label is examined.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1219DefaultSectionLastAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(OrderingRules.DefaultSectionLast);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchStatement);
    }

    /// <summary>Reports a default label that appears before the last section.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        var sections = switchStatement.Sections;
        for (var i = 0; i < sections.Count - 1; i++)
        {
            if (FindDefaultLabel(sections[i]) is { } label)
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(OrderingRules.DefaultSectionLast, label.Keyword.GetLocation()));
                return;
            }
        }
    }

    /// <summary>Finds a section's default label, if it has one.</summary>
    /// <param name="section">The switch section.</param>
    /// <returns>The default label, or <see langword="null"/>.</returns>
    private static DefaultSwitchLabelSyntax? FindDefaultLabel(SwitchSectionSyntax section)
    {
        var labels = section.Labels;
        for (var i = 0; i < labels.Count; i++)
        {
            if (labels[i] is DefaultSwitchLabelSyntax defaultLabel)
            {
                return defaultLabel;
            }
        }

        return null;
    }
}
