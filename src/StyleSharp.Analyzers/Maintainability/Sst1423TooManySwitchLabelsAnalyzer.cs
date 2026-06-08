// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports switch statements with more than the configured number of sections (SST1423).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1423TooManySwitchLabelsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default maximum number of switch sections.</summary>
    private const int DefaultMaximum = 30;

    /// <summary>The rule-specific editorconfig key.</summary>
    private const string RuleOption = "stylesharp.SST1423.max_switch_sections";

    /// <summary>The general editorconfig key.</summary>
    private const string GeneralOption = "stylesharp.max_switch_sections";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.TooManySwitchSections);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchStatement);
    }

    /// <summary>Reports a switch statement whose section count exceeds the configured maximum.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var statement = (SwitchStatementSyntax)context.Node;
        if (statement.Sections.Count <= 1)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(statement.SyntaxTree);
        var maximum = ReadMaximum(options);
        if (statement.Sections.Count <= maximum)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                MaintainabilityRules.TooManySwitchSections,
                statement.SwitchKeyword.GetLocation(),
                statement.Sections.Count,
                maximum));
    }

    /// <summary>Reads the rule-specific or general maximum, falling back to the default.</summary>
    /// <param name="options">The analyzer configuration options.</param>
    /// <returns>The positive configured maximum, or 30.</returns>
    private static int ReadMaximum(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(RuleOption, out var value) && int.TryParse(value, out var maximum) && maximum > 0)
        {
            return maximum;
        }

        return options.TryGetValue(GeneralOption, out value) && int.TryParse(value, out maximum) && maximum > 0
            ? maximum
            : DefaultMaximum;
    }
}
