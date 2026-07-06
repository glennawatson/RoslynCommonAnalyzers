// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports case labels that share a switch section with the default label (SST1466). The default
/// label already selects the shared body, so every other label in that section is redundant. A
/// switch containing any 'goto case' or 'goto default' statement is skipped entirely, because the
/// jump may target one of the labels this rule would otherwise remove.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1466RemoveCaseBesideDefaultAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RemoveCaseBesideDefault);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchStatement);
    }

    /// <summary>Reports each case label that shares a switch section with the default label.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        var gotoScanCompleted = false;
        var sections = switchStatement.Sections;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            if (labels.Count < 2 || !HasDefaultLabel(labels))
            {
                continue;
            }

            if (!gotoScanCompleted)
            {
                if (ContainsCaseTargetingGoto(switchStatement))
                {
                    return;
                }

                gotoScanCompleted = true;
            }

            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                if (!labels[labelIndex].IsKind(SyntaxKind.DefaultSwitchLabel))
                {
                    context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.RemoveCaseBesideDefault, labels[labelIndex].GetLocation()));
                }
            }
        }
    }

    /// <summary>Returns whether a switch section's label list contains the default label.</summary>
    /// <param name="labels">The section's labels.</param>
    /// <returns><see langword="true"/> when a default label is present.</returns>
    private static bool HasDefaultLabel(SyntaxList<SwitchLabelSyntax> labels)
    {
        for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
        {
            if (labels[labelIndex].IsKind(SyntaxKind.DefaultSwitchLabel))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the switch contains a 'goto case' or 'goto default' statement anywhere inside it.</summary>
    /// <param name="switchStatement">The switch statement.</param>
    /// <returns><see langword="true"/> when a label-targeting goto exists.</returns>
    private static bool ContainsCaseTargetingGoto(SwitchStatementSyntax switchStatement)
    {
        var found = false;
        DescendantTraversalHelper.VisitDescendants<GotoStatementSyntax, bool>(switchStatement, ref found, MatchCaseTargetingGoto);
        return found;
    }

    /// <summary>Records a 'goto case' or 'goto default' statement and stops the walk.</summary>
    /// <param name="gotoStatement">The candidate goto statement.</param>
    /// <param name="found">Set to <see langword="true"/> when the goto targets a switch label.</param>
    /// <returns><see langword="false"/> to stop the walk once a label-targeting goto is found.</returns>
    private static bool MatchCaseTargetingGoto(GotoStatementSyntax gotoStatement, ref bool found)
    {
        if (!gotoStatement.IsKind(SyntaxKind.GotoCaseStatement) && !gotoStatement.IsKind(SyntaxKind.GotoDefaultStatement))
        {
            return true;
        }

        found = true;
        return false;
    }
}
