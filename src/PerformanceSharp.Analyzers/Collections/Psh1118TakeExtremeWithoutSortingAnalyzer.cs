// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags LINQ chains that sort a whole sequence just to keep one extreme element (PSH1118):
/// <c>OrderBy(k).First()</c> becomes <c>MinBy(k)</c>, <c>OrderBy(k).Last()</c> becomes
/// <c>MaxBy(k)</c>, and the descending forms map the other way around; an identity selector
/// (<c>x =&gt; x</c>) maps to <c>Min()</c>/<c>Max()</c> instead. Only chains whose empty-sequence
/// behavior survives the rewrite are reported: <c>First</c>/<c>Last</c> on non-nullable
/// value-typed elements (both sides throw) and <c>FirstOrDefault</c>/<c>LastOrDefault</c> on
/// reference or nullable elements (both sides return default). <c>MinBy</c>/<c>MaxBy</c>
/// suggestions are gated on <c>Enumerable.MinBy</c> existing in the compilation (.NET 6+);
/// the identity rewrite needs no gate. A <c>ThenBy</c> between the sort and the terminal, a
/// terminal predicate, comparer overloads, and <c>Queryable</c> sources all stay silent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1118TakeExtremeWithoutSortingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The throwing first-element terminal name.</summary>
    internal const string FirstMethodName = "First";

    /// <summary>The defaulting first-element terminal name.</summary>
    internal const string FirstOrDefaultMethodName = "FirstOrDefault";

    /// <summary>The throwing last-element terminal name.</summary>
    internal const string LastMethodName = "Last";

    /// <summary>The defaulting last-element terminal name.</summary>
    internal const string LastOrDefaultMethodName = "LastOrDefault";

    /// <summary>The ascending sort method name.</summary>
    internal const string OrderByMethodName = "OrderBy";

    /// <summary>The descending sort method name.</summary>
    internal const string OrderByDescendingMethodName = "OrderByDescending";

    /// <summary>The identity-selector minimum replacement name.</summary>
    internal const string MinMethodName = "Min";

    /// <summary>The identity-selector maximum replacement name.</summary>
    internal const string MaxMethodName = "Max";

    /// <summary>The keyed minimum replacement name.</summary>
    internal const string MinByMethodName = "MinBy";

    /// <summary>The keyed maximum replacement name.</summary>
    internal const string MaxByMethodName = "MaxBy";

    /// <summary>The metadata name of the LINQ extension class.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.TakeExtremeWithoutSorting);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(EnumerableMetadataName) is not { } enumerableType)
            {
                return;
            }

            var hasMinBy = !enumerableType.GetMembers(MinByMethodName).IsEmpty;
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, enumerableType, hasMinBy), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the sort-then-take-one chain shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when a predicate-free extreme terminal directly follows a one-lambda sort.</returns>
    internal static bool IsExtremeChainShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax terminal
            && IsExtremeTerminalName(terminal.Name.Identifier.ValueText)
            && IsSingleKeySort(terminal.Expression);

    /// <summary>Computes the replacement method name for a validated chain.</summary>
    /// <param name="invocation">The terminal invocation; callers must have validated the shape.</param>
    /// <returns><c>Min</c>/<c>Max</c> for identity selectors, otherwise <c>MinBy</c>/<c>MaxBy</c>.</returns>
    internal static string GetReplacementName(InvocationExpressionSyntax invocation)
    {
        var terminal = (MemberAccessExpressionSyntax)invocation.Expression;
        var sort = (InvocationExpressionSyntax)terminal.Expression;
        var sortAccess = (MemberAccessExpressionSyntax)sort.Expression;
        var isLast = terminal.Name.Identifier.ValueText is LastMethodName or LastOrDefaultMethodName;
        var isDescending = sortAccess.Name.Identifier.ValueText == OrderByDescendingMethodName;
        var wantsMax = isLast != isDescending;
        if (IsIdentityLambda(sort.ArgumentList.Arguments[0].Expression))
        {
            return wantsMax ? MaxMethodName : MinMethodName;
        }

        return wantsMax ? MaxByMethodName : MinByMethodName;
    }

    /// <summary>Returns whether a member name is one of the four extreme-element terminals.</summary>
    /// <param name="name">The invoked member name.</param>
    /// <returns><see langword="true"/> for <c>First</c>, <c>FirstOrDefault</c>, <c>Last</c>, or <c>LastOrDefault</c>.</returns>
    private static bool IsExtremeTerminalName(string name)
        => name is FirstMethodName or FirstOrDefaultMethodName or LastMethodName or LastOrDefaultMethodName;

    /// <summary>Returns whether an expression is a one-lambda <c>OrderBy</c>/<c>OrderByDescending</c> invocation.</summary>
    /// <param name="expression">The terminal's receiver expression.</param>
    /// <returns><see langword="true"/> when the receiver is a single-key sort call.</returns>
    private static bool IsSingleKeySort(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax sort
            && sort.ArgumentList.Arguments.Count == 1
            && sort.Expression is MemberAccessExpressionSyntax sortAccess
            && sortAccess.Name.Identifier.ValueText is OrderByMethodName or OrderByDescendingMethodName
            && IsKeySelectorLambda(sort.ArgumentList.Arguments[0].Expression);

    /// <summary>Returns whether an expression is a one-parameter key-selector lambda.</summary>
    /// <param name="expression">The candidate selector expression.</param>
    /// <returns><see langword="true"/> for simple lambdas and one-parameter parenthesized lambdas.</returns>
    private static bool IsKeySelectorLambda(ExpressionSyntax expression)
        => expression is SimpleLambdaExpressionSyntax
            or ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 };

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

    /// <summary>Returns whether the extreme scan reacts to an empty sequence exactly like the terminal.</summary>
    /// <param name="terminalName">The terminal method name.</param>
    /// <param name="elementType">The sequence element type.</param>
    /// <returns><see langword="true"/> when both sides throw, or both sides return default.</returns>
    private static bool EmptyBehaviorMatches(string terminalName, ITypeSymbol elementType)
    {
        var isNullableValue = elementType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        return terminalName is FirstMethodName or LastMethodName
            ? elementType.IsValueType && !isNullableValue
            : elementType.IsReferenceType || isNullableValue;
    }

    /// <summary>Reports PSH1118 for a sort-then-take-one chain that binds to the LINQ extension class.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The LINQ extension class.</param>
    /// <param name="hasMinBy">Whether the compilation's <c>Enumerable</c> exposes <c>MinBy</c>.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType, bool hasMinBy)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsExtremeChainShape(invocation))
        {
            return;
        }

        var replacement = GetReplacementName(invocation);
        if (!hasMinBy && replacement is MinByMethodName or MaxByMethodName)
        {
            return;
        }

        var terminal = (MemberAccessExpressionSyntax)invocation.Expression;
        var sort = (InvocationExpressionSyntax)terminal.Expression;
        if (!BindsToReportableChain(context, invocation, terminal, sort, enumerableType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.TakeExtremeWithoutSorting,
            terminal.Name.GetLocation(),
            replacement));
    }

    /// <summary>Returns whether both chain calls bind to the LINQ extension class and the empty-sequence behavior survives the rewrite.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The terminal invocation.</param>
    /// <param name="terminal">The terminal member access.</param>
    /// <param name="sort">The sort invocation.</param>
    /// <param name="enumerableType">The LINQ extension class.</param>
    /// <returns><see langword="true"/> when the chain is safe to report.</returns>
    private static bool BindsToReportableChain(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax terminal,
        InvocationExpressionSyntax sort,
        INamedTypeSymbol enumerableType)
        => context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol terminalMethod
            && SymbolEqualityComparer.Default.Equals(terminalMethod.ContainingType, enumerableType)
            && terminalMethod.TypeArguments is [{ } elementType]
            && context.SemanticModel.GetSymbolInfo(sort, context.CancellationToken).Symbol is IMethodSymbol sortMethod
            && SymbolEqualityComparer.Default.Equals(sortMethod.ContainingType, enumerableType)
            && EmptyBehaviorMatches(terminal.Name.Identifier.ValueText, elementType);
}
