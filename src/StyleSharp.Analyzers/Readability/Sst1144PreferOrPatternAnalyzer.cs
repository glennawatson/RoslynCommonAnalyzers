// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a switch section with several stacked case labels that could be combined into a single
/// <c>case A or B:</c> pattern (SST1144, opt-in). The rule is gated on C# 9 (where <c>or</c>
/// patterns arrived) and never fires on a <c>default</c> label or a label with a <c>when</c>
/// guard, which cannot be merged, nor where the one line the merge produces would run past the
/// configured maximum.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1144PreferOrPatternAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric value of <c>LanguageVersion.CSharp9</c>, the first version with <c>or</c> patterns.</summary>
    private const LanguageVersion CSharp9 = LanguageVersion.CSharp9;

    /// <summary>The width of the <c>case </c> the merged label opens with.</summary>
    private const int CaseKeywordWidth = 5;

    /// <summary>The width of the <c> or </c> that joins two merged patterns.</summary>
    private const int OrSeparatorWidth = 4;

    /// <summary>The width of the colon the merged label closes with.</summary>
    private const int ColonWidth = 1;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ReadabilityRules.PreferOrPattern);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

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
    internal static bool IsCombinable(SwitchLabelSyntax label) =>
        label is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax { WhenClause: null };

    /// <summary>Reports SST1144 when every label of a multi-label section can be combined.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { } options || options.LanguageVersion < CSharp9)
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

        if (!MergedLabelFits(context, labels))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.PreferOrPattern, labels[0].GetLocation()));
    }

    /// <summary>Returns whether the one line the merge produces stays within the layout ceiling.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="labels">The section's labels.</param>
    /// <returns><see langword="true"/> when the merged label fits.</returns>
    /// <remarks>
    /// Several labels that each fit on their own line can be one label that does not. Reporting that is
    /// asking for a line the layout rules reject, so the merge yields.
    /// </remarks>
    private static bool MergedLabelFits(in SyntaxNodeAnalysisContext context, SyntaxList<SwitchLabelSyntax> labels)
    {
        var tree = context.Node.SyntaxTree;
        var length = tree.GetLineSpan(labels[0].Span, context.CancellationToken).StartLinePosition.Character
            + CaseKeywordWidth
            + ((labels.Count - 1) * OrSeparatorWidth)
            + ColonWidth;

        for (var i = 0; i < labels.Count; i++)
        {
            length += PatternWidth(labels[i]);
        }

        return LineLengthBudget.Fits(length, tree, context.Options, context.Compilation, context.CancellationToken);
    }

    /// <summary>Returns the width the label contributes to the merged pattern.</summary>
    /// <param name="label">The switch label.</param>
    /// <returns>The width of the label's pattern, without its <c>case</c> and colon.</returns>
    private static int PatternWidth(SwitchLabelSyntax label) => label switch
    {
        CasePatternSwitchLabelSyntax pattern => pattern.Pattern.Span.Length,
        CaseSwitchLabelSyntax value => value.Value.Span.Length,
        _ => 0
    };
}
