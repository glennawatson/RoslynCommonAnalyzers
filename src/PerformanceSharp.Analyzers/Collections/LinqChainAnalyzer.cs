// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports LINQ chain orderings that repeat or discard enumeration work, in a
/// single invocation walk with syntax-first candidate filtering.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>PSH1107 — a filter applied after a sort pays to order discarded elements.</description></item>
/// <item><description>PSH1108 — a second OrderBy discards the first sort instead of refining it.</description></item>
/// <item><description>PSH1109 — consecutive Where calls stack iterator layers.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LinqChainAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CollectionRules.FilterBeforeSort,
        CollectionRules.UseThenBy,
        CollectionRules.MergeConsecutiveWhere);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports LINQ chain reorderings and merges for one invocation.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: { } name, Expression: InvocationExpressionSyntax receiver }
            || receiver.Expression is not MemberAccessExpressionSyntax { Name: { } receiverName })
        {
            return;
        }

        var nameText = name.Identifier.ValueText;
        if (nameText == "Where")
        {
            AnalyzeWhere(context, invocation, name, receiver, receiverName.Identifier.ValueText);
        }
        else if (nameText is "OrderBy" or "OrderByDescending")
        {
            AnalyzeRepeatedSort(
                context,
                invocation,
                name,
                receiver,
                receiverName.Identifier.ValueText,
                nameText == "OrderBy" ? "ThenBy" : "ThenByDescending");
        }
    }

    /// <summary>Reports a <c>Where</c> that follows a sort (PSH1107) or another <c>Where</c> (PSH1109).</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="invocation">The <c>Where</c> invocation.</param>
    /// <param name="name">The <c>Where</c> method name.</param>
    /// <param name="receiver">The receiver invocation.</param>
    /// <param name="receiverName">The receiver invocation's method name.</param>
    private static void AnalyzeWhere(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax name,
        InvocationExpressionSyntax receiver,
        string receiverName)
    {
        if (!LinqCallSyntax.TryGetOneParameterLambda(invocation, out var outerLambda))
        {
            return;
        }

        if (LinqCallSyntax.IsSortMethodName(receiverName))
        {
            ReportFilterAfterSort(context, invocation, name, receiver);
            return;
        }

        if (receiverName != "Where")
        {
            return;
        }

        ReportConsecutiveWhere(context, invocation, name, receiver, outerLambda);
    }

    /// <summary>Reports a <c>Where</c> whose receiver is a LINQ sort (PSH1107).</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="invocation">The <c>Where</c> invocation.</param>
    /// <param name="name">The <c>Where</c> method name.</param>
    /// <param name="receiver">The sort invocation.</param>
    private static void ReportFilterAfterSort(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax name,
        InvocationExpressionSyntax receiver)
    {
        if (!LinqCallSyntax.TryGetOneParameterLambda(receiver, out _)
            || !AreEnumerableInvocations(invocation, receiver, context))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CollectionRules.FilterBeforeSort, name.GetLocation()));
    }

    /// <summary>Reports a <c>Where</c> whose receiver is another expression-bodied <c>Where</c> (PSH1109).</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="invocation">The outer <c>Where</c> invocation.</param>
    /// <param name="name">The outer <c>Where</c> method name.</param>
    /// <param name="receiver">The inner <c>Where</c> invocation.</param>
    /// <param name="outerLambda">The outer <c>Where</c> predicate.</param>
    private static void ReportConsecutiveWhere(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax name,
        InvocationExpressionSyntax receiver,
        LambdaExpressionSyntax outerLambda)
    {
        if (outerLambda.ExpressionBody is null
            || !LinqCallSyntax.TryGetOneParameterLambda(receiver, out var innerLambda)
            || innerLambda.ExpressionBody is null
            || !AreEnumerableInvocations(invocation, receiver, context))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CollectionRules.MergeConsecutiveWhere, name.GetLocation()));
    }

    /// <summary>Reports an <c>OrderBy</c>/<c>OrderByDescending</c> applied to an already sorted sequence (PSH1108).</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="invocation">The repeated sort invocation.</param>
    /// <param name="name">The repeated sort's method name.</param>
    /// <param name="receiver">The receiver invocation.</param>
    /// <param name="receiverName">The receiver invocation's method name.</param>
    /// <param name="refiningName">The refining method name to suggest.</param>
    private static void AnalyzeRepeatedSort(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax name,
        InvocationExpressionSyntax receiver,
        string receiverName,
        string refiningName)
    {
        if (!LinqCallSyntax.IsSortMethodName(receiverName)
            || !LinqCallSyntax.TryGetOneParameterLambda(invocation, out _)
            || !LinqCallSyntax.TryGetOneParameterLambda(receiver, out _)
            || !AreEnumerableInvocations(invocation, receiver, context))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CollectionRules.UseThenBy, name.GetLocation(), refiningName));
    }

    /// <summary>Returns whether both chain links resolve to <see cref="System.Linq.Enumerable"/>.</summary>
    /// <param name="outer">The outer invocation.</param>
    /// <param name="inner">The inner (receiver) invocation.</param>
    /// <param name="context">The syntax context.</param>
    /// <returns><see langword="true"/> when both calls target in-memory LINQ methods.</returns>
    private static bool AreEnumerableInvocations(
        InvocationExpressionSyntax outer,
        InvocationExpressionSyntax inner,
        SyntaxNodeAnalysisContext context)
        => EnumerableInvocationHelper.IsEnumerableInvocation(outer, context.SemanticModel, context.CancellationToken)
            && EnumerableInvocationHelper.IsEnumerableInvocation(inner, context.SemanticModel, context.CancellationToken);
}
