// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a field or local whose initializer <c>=</c> wraps onto the wrong side of its line break
/// (SST1528), configured with <c>stylesharp.equals_token_new_line</c> (<c>after</c> | <c>before</c>;
/// default <c>after</c>). Only a variable-declarator initializer with an adjacent line break is checked;
/// property initializers, parameter defaults, and single-line initializers are never touched.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1528EqualsTokenNewLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key for the equals-sign placement (SST1528).</summary>
    internal const string SpecificKey = "stylesharp.SST1528.equals_token_new_line";

    /// <summary>General editorconfig key for the equals-sign placement.</summary>
    internal const string GeneralKey = "stylesharp.equals_token_new_line";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.EqualsTokenNewLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsValueClause);
    }

    /// <summary>Reports a wrapped initializer equals sign on the wrong side of its line break.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var clause = (EqualsValueClauseSyntax)context.Node;
        if (clause.Parent is not VariableDeclaratorSyntax)
        {
            return;
        }

        var equals = clause.EqualsToken;
        var breakBefore = LayoutHelpers.HasLineBreakBefore(equals);
        var breakAfter = LayoutHelpers.HasLineBreakAfter(equals);
        if (!breakBefore && !breakAfter)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(clause.SyntaxTree);
        var wantBreakBefore = LayoutStyleOptions.ReadBreakBefore(options, SpecificKey, GeneralKey, defaultBreakBefore: false);
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            LayoutRules.EqualsTokenNewLine,
            equals.GetLocation(),
            LayoutHelpers.PlacementProperties(wantBreakBefore),
            wantBreakBefore ? "start" : "end"));
    }
}
