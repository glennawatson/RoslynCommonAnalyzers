// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports a conditional expression nested directly in another conditional expression (SST1147).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1147NestedTernaryAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.NoNestedTernary);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConditionalExpression);
    }

    /// <summary>Reports a conditional whose nearest enclosing expression is another conditional.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var parent = context.Node.Parent;
        while (parent is ParenthesizedExpressionSyntax)
        {
            parent = parent.Parent;
        }

        if (parent is not ConditionalExpressionSyntax)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.NoNestedTernary, context.Node.GetLocation()));
    }
}
