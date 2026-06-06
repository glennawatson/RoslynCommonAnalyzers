// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a switch section with several stacked case labels that could be combined into a single
/// <c>case A or B:</c> pattern (SST1144, opt-in). The rule is gated on C# 9 (where <c>or</c>
/// patterns arrived) and never fires on a <c>default</c> label or a label with a <c>when</c>
/// guard, which cannot be merged.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferOrPatternAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric value of <c>LanguageVersion.CSharp9</c>, the first version with <c>or</c> patterns.</summary>
    private const int CSharp9 = 900;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.PreferOrPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchSection);
    }

    /// <summary>Returns whether a switch label can be merged into an <c>or</c> pattern.</summary>
    /// <param name="label">The switch label.</param>
    /// <returns><see langword="true"/> for a value label or a guard-free pattern label.</returns>
    internal static bool IsCombinable(SwitchLabelSyntax label)
        => label is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax { WhenClause: null };

    /// <summary>Reports SST1144 when every label of a multi-label section can be combined.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { } options || (int)options.LanguageVersion < CSharp9)
        {
            return;
        }

        var labels = ((SwitchSectionSyntax)context.Node).Labels;
        if (labels.Count < 2)
        {
            return;
        }

        for (var i = 0; i < labels.Count; i++)
        {
            if (!IsCombinable(labels[i]))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.PreferOrPattern, labels[0].GetLocation()));
    }
}
