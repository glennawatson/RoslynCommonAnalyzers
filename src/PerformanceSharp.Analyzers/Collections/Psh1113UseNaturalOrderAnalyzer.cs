// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>OrderBy(x =&gt; x)</c> and <c>OrderByDescending(x =&gt; x)</c> on
/// <c>System.Linq.Enumerable</c> (PSH1113), where <c>Order()</c>/<c>OrderDescending()</c>
/// compare elements directly without a key-selector delegate. The whole rule is gated on
/// <c>Enumerable.Order</c> existing in the compilation (.NET 7+), so it costs nothing on older
/// frameworks. A trailing comparer argument is preserved by the code fix; <c>Queryable</c>
/// sources are skipped because rewriting expression trees changes what a provider sees.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1113UseNaturalOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ascending sort method name.</summary>
    internal const string OrderByMethodName = "OrderBy";

    /// <summary>The descending sort method name.</summary>
    internal const string OrderByDescendingMethodName = "OrderByDescending";

    /// <summary>The ascending replacement method name.</summary>
    internal const string OrderMethodName = "Order";

    /// <summary>The descending replacement method name.</summary>
    internal const string OrderDescendingMethodName = "OrderDescending";

    /// <summary>The argument count of the sort overload that carries a comparer.</summary>
    internal const int SelectorAndComparerArgumentCount = 2;

    /// <summary>The metadata name of the LINQ extension class.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseNaturalOrder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(EnumerableMetadataName) is not { } enumerableType
                || enumerableType.GetMembers(OrderMethodName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, enumerableType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the identity-selector sort shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when an OrderBy/OrderByDescending call passes an identity lambda first.</returns>
    internal static bool IsIdentitySortShape(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText is OrderByMethodName or OrderByDescendingMethodName
            && invocation.ArgumentList.Arguments.Count is 1 or SelectorAndComparerArgumentCount
            && IsIdentityLambda(invocation.ArgumentList.Arguments[0].Expression);

    /// <summary>Returns whether an expression is a lambda that returns its own single parameter.</summary>
    /// <param name="expression">The candidate selector expression.</param>
    /// <returns><see langword="true"/> for <c>x =&gt; x</c> in simple or parenthesized form.</returns>
    private static bool IsIdentityLambda(ExpressionSyntax expression)
        => expression switch
        {
            SimpleLambdaExpressionSyntax simple =>
                simple.ExpressionBody is IdentifierNameSyntax body
                    && body.Identifier.ValueText == simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.ParameterList.Parameters.Count == 1
                    && parenthesized.ExpressionBody is IdentifierNameSyntax body
                    && body.Identifier.ValueText == parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => false,
        };

    /// <summary>Reports PSH1113 for an identity-selector sort that binds to the LINQ extension class.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The LINQ extension class.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsIdentitySortShape(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, enumerableType))
        {
            return;
        }

        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name;
        var isDescending = name.Identifier.ValueText == OrderByDescendingMethodName;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseNaturalOrder,
            name.GetLocation(),
            isDescending ? OrderDescendingMethodName : OrderMethodName,
            name.Identifier.ValueText));
    }
}
