// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a switch section with more code lines than the configured maximum (SST1524), which defaults to 20
/// and is configured with <c>stylesharp.SST1524.max_switch_section_lines</c>.
/// </summary>
/// <remarks>
/// The section is measured from its first label to its last statement, counting only lines that carry code,
/// so a heavily commented case is not penalised for explaining itself. Switch <em>expression</em> arms are not
/// measured: an arm is one expression by construction, and the language already stops it from growing into a
/// procedure. The section's raw line span bounds its code lines, so a short section is rejected on two line
/// lookups.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1524SwitchSectionTooLongAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.SwitchSectionTooLong);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchSection);
    }

    /// <summary>Measures one switch section and reports it when its code lines exceed the maximum.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var section = (SwitchSectionSyntax)context.Node;
        if (section.Labels.Count == 0)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(section.SyntaxTree);
        var maximum = SizeLimitOptions.ReadMaxSwitchSectionLines(options);
        var text = section.SyntaxTree.GetText(context.CancellationToken);
        if (CodeLineCounter.SpannedLines(text, section.Span) <= maximum)
        {
            return;
        }

        var codeLines = CodeLineCounter.Count(text, section);
        if (codeLines <= maximum)
        {
            return;
        }

        var location = section.Labels[0].GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.SwitchSectionTooLong, location, codeLines, maximum));
    }
}
