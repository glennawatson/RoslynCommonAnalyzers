// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests adding the <c>static</c> modifier to anonymous functions that capture no
/// state (PSH1000). A lambda or anonymous method qualifies only when flow analysis
/// proves it captures nothing — no locals, no enclosing-method parameters, and no
/// <c>this</c> — so adding the modifier cannot break compilation. Functions converted
/// to <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> are skipped because
/// static anonymous functions are illegal in expression trees, and files parsed as
/// C# 8 or earlier are skipped because the modifier does not exist there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1000StaticAnonymousFunctionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the expression-tree delegate wrapper type.</summary>
    private const string ExpressionOfTMetadataName = "System.Linq.Expressions.Expression`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.MakeAnonymousFunctionStatic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var expressionOfTType = start.Compilation.GetTypeByMetadataName(ExpressionOfTMetadataName);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAnonymousFunction(nodeContext, expressionOfTType),
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression,
                SyntaxKind.AnonymousMethodExpression);
        });
    }

    /// <summary>Returns whether an anonymous function passes the syntax-only candidate checks.</summary>
    /// <param name="function">The anonymous function to inspect.</param>
    /// <returns><see langword="true"/> when the function is not already static and the language version allows the modifier.</returns>
    internal static bool IsSyntaxCandidate(AnonymousFunctionExpressionSyntax function)
        => !ModifierListHelper.Contains(function.Modifiers, SyntaxKind.StaticKeyword)
            && ((CSharpParseOptions)function.SyntaxTree.Options).LanguageVersion >= LanguageVersion.CSharp9;

    /// <summary>Returns the small leading span the diagnostic is reported on.</summary>
    /// <param name="function">The anonymous function to report.</param>
    /// <returns>The parameter (list) span for lambdas, or the <c>delegate</c> keyword span for anonymous methods.</returns>
    internal static TextSpan GetReportSpan(AnonymousFunctionExpressionSyntax function) => function switch
    {
        SimpleLambdaExpressionSyntax simple => simple.Parameter.Span,
        ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Span,
        AnonymousMethodExpressionSyntax anonymousMethod => anonymousMethod.DelegateKeyword.Span,
        _ => function.Span
    };

    /// <summary>Reports PSH1000 for an anonymous function that provably captures nothing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expressionOfTType">The compilation's <c>Expression&lt;TDelegate&gt;</c> type, when it exists.</param>
    private static void AnalyzeAnonymousFunction(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionOfTType)
    {
        var function = (AnonymousFunctionExpressionSyntax)context.Node;
        if (!IsSyntaxCandidate(function)
            || IsExpressionTreeConversion(context.SemanticModel, function, expressionOfTType, context.CancellationToken)
            || !HasNoCaptures(context.SemanticModel, function))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.MakeAnonymousFunctionStatic,
            function.SyntaxTree,
            GetReportSpan(function)));
    }

    /// <summary>Returns whether the anonymous function converts to an expression tree, where <c>static</c> is illegal.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="function">The anonymous function to inspect.</param>
    /// <param name="expressionOfTType">The compilation's <c>Expression&lt;TDelegate&gt;</c> type, when it exists.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the function's converted type is constructed from <c>Expression&lt;TDelegate&gt;</c>.</returns>
    private static bool IsExpressionTreeConversion(
        SemanticModel model,
        AnonymousFunctionExpressionSyntax function,
        INamedTypeSymbol? expressionOfTType,
        CancellationToken cancellationToken)
        => expressionOfTType is not null
            && model.GetTypeInfo(function, cancellationToken).ConvertedType is INamedTypeSymbol convertedType
            && SymbolEqualityComparer.Default.Equals(convertedType.ConstructedFrom, expressionOfTType);

    /// <summary>Returns whether flow analysis proves the anonymous function captures nothing, including <c>this</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="function">The anonymous function to inspect.</param>
    /// <returns><see langword="true"/> when the captured-variable sets are provably empty.</returns>
    /// <remarks>
    /// Only <see cref="DataFlowAnalysis.CapturedInside"/> answers the question. <c>Captured</c> is
    /// <c>CapturedInside</c> together with <c>CapturedOutside</c>, and <c>CapturedOutside</c> holds what the
    /// <em>other</em> lambdas in the enclosing method captured. Reading it made one capturing lambda hide
    /// every capture-free sibling in the same statement: the rule went quiet on exactly the lambdas it
    /// exists to find, and only where a neighbour happened to close over something.
    /// </remarks>
    private static bool HasNoCaptures(SemanticModel model, AnonymousFunctionExpressionSyntax function)
    {
        var dataFlow = model.AnalyzeDataFlow(function);
        return dataFlow is { Succeeded: true, CapturedInside.IsEmpty: true };
    }
}
