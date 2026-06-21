// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a switch statement whose sections all return or throw a value, where a C# 8 switch
/// expression keeps the same mapping in expression form (SST2201).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2201PreferSwitchExpressionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 8 language-version value.</summary>
    private const int CSharp8 = 800;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.PreferSwitchExpression);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchStatement);
    }

    /// <summary>Returns whether the switch statement can be represented as a switch expression without introducing statement blocks.</summary>
    /// <param name="switchStatement">The switch statement to inspect.</param>
    /// <returns><see langword="true"/> when every section is a single return/throw arm and a default arm exists.</returns>
    internal static bool IsReturnOnlySwitchExpressionCandidate(SwitchStatementSyntax switchStatement)
    {
        var sections = switchStatement.Sections;
        if (sections.Count < 2)
        {
            return false;
        }

        var hasDefault = false;
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (section.Labels.Count != 1 || section.Statements.Count != 1 || !IsSwitchExpressionArmStatement(section.Statements[0]))
            {
                return false;
            }

            hasDefault |= section.Labels[0].IsKind(SyntaxKind.DefaultSwitchLabel);
        }

        return hasDefault;
    }

    /// <summary>Reports SST2201 for return-only switch statements in C# 8 or later.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        if (switchStatement.SyntaxTree.Options is not CSharpParseOptions options
            || (int)options.LanguageVersion < CSharp8
            || !IsReturnOnlySwitchExpressionCandidate(switchStatement))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.PreferSwitchExpression, switchStatement.SwitchKeyword.GetLocation()));
    }

    /// <summary>Returns whether the section statement can be represented as a switch expression arm expression.</summary>
    /// <param name="statement">The switch-section statement.</param>
    /// <returns><see langword="true"/> for <c>return value;</c> and <c>throw value;</c>.</returns>
    private static bool IsSwitchExpressionArmStatement(StatementSyntax statement)
        => statement switch
        {
            ReturnStatementSyntax { Expression: not null } => true,
            ThrowStatementSyntax { Expression: not null } => true,
            _ => false
        };
}
