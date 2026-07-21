// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a lambda whose block body is a single <c>return expr;</c> — <c>x =&gt; { return expr; }</c> — and can
/// be written with an expression body <c>x =&gt; expr</c> (SST2257).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2257SimplifyLambdaBodyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.SimplifyLambdaBody);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleLambdaExpression);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ParenthesizedLambdaExpression);
    }

    /// <summary>Returns the single returned expression when a lambda's block body is one <c>return expr;</c>.</summary>
    /// <param name="lambda">The lambda to inspect.</param>
    /// <param name="expression">The returned expression.</param>
    /// <returns><see langword="true"/> when the block body is a single value-returning statement.</returns>
    internal static bool TryGetReturnExpression(LambdaExpressionSyntax lambda, out ExpressionSyntax expression)
    {
        if (lambda.Body is BlockSyntax { Statements.Count: 1 } block
            && block.Statements[0] is ReturnStatementSyntax { Expression: { } returned })
        {
            expression = returned;
            return true;
        }

        expression = null!;
        return false;
    }

    /// <summary>Reports a lambda whose block body is a single value-returning statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var lambda = (LambdaExpressionSyntax)context.Node;
        if (!TryGetReturnExpression(lambda, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.SimplifyLambdaBody, lambda.ArrowToken.GetLocation()));
    }
}
