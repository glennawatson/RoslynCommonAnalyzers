// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a switch section that carries more than one statement without wrapping them in braces
/// (SST1525). Several bare statements cannot share a single brace, so any section with two or more
/// top-level statements is unbraced by construction; a section with a single statement — including a
/// single brace-delimited block — is left alone. This extends the always-braces house style to switch
/// sections.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1525SwitchSectionBracesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.SwitchSectionBraces);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchSection);
    }

    /// <summary>Reports a switch section whose body is more than one bare statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var section = (SwitchSectionSyntax)context.Node;
        if (section.Statements.Count <= 1 || section.Labels.Count == 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.SwitchSectionBraces, section.Labels[0].GetLocation()));
    }
}
