// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a control-flow statement whose child statement is not wrapped in braces (SST1503),
/// whether the child is on one line or many. An <c>else if</c> chain is not treated as an
/// unbraced child. This is the strict always-braces rule; the repository disables it by
/// default in favour of SST1519/SST1520 plus the analyzer's the rule.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1503RequireBracesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.BracesRequired);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, LayoutHelpers.EmbeddedStatementKinds());
    }

    /// <summary>Reports an embedded statement that omits its braces.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!LayoutHelpers.TryGetEmbeddedStatement(context.Node, out var child) || child is BlockSyntax or IfStatementSyntax)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesRequired, context.Node.GetFirstToken().GetLocation()));
    }
}
