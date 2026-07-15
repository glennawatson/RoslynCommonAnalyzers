// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports enum switch statements that look like explicit value mappings but omit enum members.
/// This is quieter than the older broad statement-switch coverage rule: a default section counts
/// as intentional, and switches with fall-through-shaped empty sections are skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2242EnumSwitchStatementMappingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.CompleteEnumSwitchStatementMapping);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchStatement);
    }

    /// <summary>Reports an enum switch statement with missing named members.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        if (HasDefaultOrFallthroughShape(switchStatement)
            || context.SemanticModel.GetTypeInfo(switchStatement.Expression, context.CancellationToken).Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            || CoversEveryEnumValue(enumType, switchStatement, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.CompleteEnumSwitchStatementMapping, switchStatement.SwitchKeyword.GetLocation()));
    }

    /// <summary>Returns whether a switch has a default label or empty fall-through section.</summary>
    /// <param name="switchStatement">The switch statement.</param>
    /// <returns><see langword="true"/> when the switch is intentionally catch-all or not a simple mapping.</returns>
    private static bool HasDefaultOrFallthroughShape(SwitchStatementSyntax switchStatement)
    {
        var sections = switchStatement.Sections;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            if (sections[sectionIndex].Statements.Count == 0)
            {
                return true;
            }

            var labels = sections[sectionIndex].Labels;
            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                if (labels[labelIndex].IsKind(SyntaxKind.DefaultSwitchLabel))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether every enum value has an explicit case label.</summary>
    /// <param name="enumType">The enum type.</param>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when all named enum values are covered.</returns>
    private static bool CoversEveryEnumValue(
        INamedTypeSymbol enumType,
        SwitchStatementSyntax switchStatement,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var members = enumType.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (EnumSwitchCoverage.IsEnumValue(members[i], out var field)
                && !EnumSwitchCoverage.IsCaseLabelCovered(field, switchStatement, model, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}
