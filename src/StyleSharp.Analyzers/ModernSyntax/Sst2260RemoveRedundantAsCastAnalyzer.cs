// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>as</c> conversion to a type the operand already has (SST2260): <c>value as string</c> where
/// <c>value</c> is statically a <c>string</c> can never return null and never changes the value or its type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2260RemoveRedundantAsCastAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.RemoveRedundantAsCast);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AsExpression);
    }

    /// <summary>Reports an <c>as</c> cast whose operand already has the target type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var expression = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var operandType = model.GetTypeInfo(expression.Left, context.CancellationToken).Type;
        if (operandType is null || operandType.TypeKind == TypeKind.Error)
        {
            return;
        }

        var targetType = model.GetTypeInfo(expression, context.CancellationToken).Type;
        if (targetType is null || targetType.TypeKind == TypeKind.Error || !SymbolEqualityComparer.Default.Equals(operandType, targetType))
        {
            return;
        }

        var typeName = targetType.ToMinimalDisplayString(model, expression.SpanStart);
        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.RemoveRedundantAsCast, expression.GetLocation(), typeName));
    }
}
