// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports LINQ usage that costs avoidable enumeration work, in a single
/// invocation walk with syntax-first candidate filtering.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>PSH1100 — hot-path code should avoid LINQ extension methods (opt-in).</description></item>
/// <item><description>PSH1101 — a LINQ terminal call can carry the preceding Where predicate.</description></item>
/// <item><description>PSH1102 — a LINQ type check followed by Cast can use one typed filter.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LinqUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Editorconfig key that enables LINQ diagnostics on performance-sensitive paths.</summary>
    private const string AvoidLinqOnHotPathKey = "performancesharp.avoid_linq_on_hot_path";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CollectionRules.AvoidLinqOnHotPath,
        CollectionRules.CollapseLinqWhereTerminal,
        CollectionRules.CollapseLinqTypeFilter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports LINQ chain simplifications and hot-path LINQ calls for one invocation.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetWhereTerminalCollapse(invocation, context.SemanticModel, context.CancellationToken, out var terminalName))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(CollectionRules.CollapseLinqWhereTerminal, terminalName.GetLocation()));
            return;
        }

        if (TryGetTypeFilterCollapse(invocation, context.SemanticModel, context.CancellationToken, out var castName))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(CollectionRules.CollapseLinqTypeFilter, castName!.GetLocation()));
            return;
        }

        if (!IsAvoidLinqOnHotPathEnabled(context)
            || !TryGetInvocationName(invocation, out var name)
            || !IsHotPathLinqMethodName(name.Identifier.ValueText)
            || !EnumerableInvocationHelper.IsEnumerableInvocation(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CollectionRules.AvoidLinqOnHotPath, name.GetLocation()));
    }

    /// <summary>Finds a <c>Where(predicate).Any()</c> or <c>Where(predicate).Count()</c> chain.</summary>
    /// <param name="invocation">The terminal invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="terminalName">The terminal method name.</param>
    /// <returns><see langword="true"/> when the chain can collapse into a predicate terminal call.</returns>
    private static bool TryGetWhereTerminalCollapse(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out SimpleNameSyntax terminalName)
    {
        terminalName = null!;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: { } name, Expression: InvocationExpressionSyntax whereInvocation }
            || !IsPredicateTerminalName(name.Identifier.ValueText)
            || whereInvocation.ArgumentList.Arguments.Count != 1
            || whereInvocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Where" })
        {
            return false;
        }

        if (!EnumerableInvocationHelper.IsEnumerableInvocation(invocation, model, cancellationToken)
            || !EnumerableInvocationHelper.IsEnumerableInvocation(whereInvocation, model, cancellationToken))
        {
            return false;
        }

        terminalName = name;
        return true;
    }

    /// <summary>Finds a <c>Where(x =&gt; x is T).Cast&lt;T&gt;()</c> chain.</summary>
    /// <param name="invocation">The cast invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="castName">The cast method name.</param>
    /// <returns><see langword="true"/> when the chain can be represented as one typed filter call.</returns>
    private static bool TryGetTypeFilterCollapse(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out GenericNameSyntax? castName)
    {
        castName = null;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Cast" } name, Expression: InvocationExpressionSyntax whereInvocation }
            || name.TypeArgumentList.Arguments.Count != 1
            || whereInvocation.ArgumentList.Arguments.Count != 1
            || whereInvocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Where" }
            || !TryGetLambdaTypeCheck(whereInvocation.ArgumentList.Arguments[0].Expression, out var checkedType))
        {
            return false;
        }

        var castType = name.TypeArgumentList.Arguments[0];
        if (!SyntaxFactory.AreEquivalent(checkedType, castType)
            || !EnumerableInvocationHelper.IsEnumerableInvocation(invocation, model, cancellationToken)
            || !EnumerableInvocationHelper.IsEnumerableInvocation(whereInvocation, model, cancellationToken))
        {
            return false;
        }

        castName = name;
        return true;
    }

    /// <summary>Gets a simple lambda body of the form <c>x is T</c>.</summary>
    /// <param name="expression">The lambda expression.</param>
    /// <param name="checkedType">The checked type.</param>
    /// <returns><see langword="true"/> when the lambda is a direct type filter.</returns>
    private static bool TryGetLambdaTypeCheck(ExpressionSyntax expression, out TypeSyntax checkedType)
    {
        checkedType = null!;
        return TryGetTypePattern(
            expression switch
            {
                SimpleLambdaExpressionSyntax simple => simple.ExpressionBody,
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ExpressionBody,
                _ => null
            },
            out checkedType);
    }

    /// <summary>Gets the type from a direct type pattern expression.</summary>
    /// <param name="body">The lambda body.</param>
    /// <param name="checkedType">The checked type.</param>
    /// <returns><see langword="true"/> when the body is a simple type pattern.</returns>
    private static bool TryGetTypePattern(ExpressionSyntax? body, out TypeSyntax checkedType)
    {
        checkedType = null!;
        if (body is not IsPatternExpressionSyntax patternExpression)
        {
            return TryGetLegacyIsType(body, out checkedType);
        }

        switch (patternExpression.Pattern)
        {
            case DeclarationPatternSyntax declarationPattern:
                {
                    checkedType = declarationPattern.Type;
                    return true;
                }

            case RecursivePatternSyntax { Type: { } type }:
                {
                    checkedType = type;
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Gets the type from a legacy <c>is T</c> expression.</summary>
    /// <param name="body">The expression body.</param>
    /// <param name="checkedType">The checked type.</param>
    /// <returns><see langword="true"/> when the body is a legacy type check.</returns>
    private static bool TryGetLegacyIsType(ExpressionSyntax? body, out TypeSyntax checkedType)
    {
        checkedType = null!;
        if (body is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax type })
        {
            return false;
        }

        checkedType = type;
        return true;
    }

    /// <summary>Gets an invocation method name without allocating strings for non-member shapes.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when a simple method name was found.</returns>
    private static bool TryGetInvocationName(InvocationExpressionSyntax invocation, out SimpleNameSyntax name)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax { Name: { } memberName }:
                {
                    name = memberName;
                    return true;
                }

            case IdentifierNameSyntax identifier:
                {
                    name = identifier;
                    return true;
                }

            case GenericNameSyntax generic:
                {
                    name = generic;
                    return true;
                }

            default:
                {
                    name = null!;
                    return false;
                }
        }
    }

    /// <summary>Returns whether the method name is a common <see cref="System.Linq.Enumerable"/> operator worth screening semantically.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when the method can be a LINQ iterator or terminal call.</returns>
    [SuppressMessage("Critical Code Smell", "S1541:Methods and properties should not be too complex", Justification = "A flat name switch avoids an allocation-heavy lookup on every invocation.")]
    private static bool IsHotPathLinqMethodName(string name)
        => name switch
        {
            "Aggregate" or "All" or "Any" or "Append" or "Cast" or "Concat" or "Contains" or "Count" or "DefaultIfEmpty"
                or "Distinct" or "ElementAt" or "Except" or "First" or "FirstOrDefault" or "GroupBy" or "Intersect"
                or "Last" or "LastOrDefault" or "Max" or "Min" or "OfType" or "OrderBy" or "OrderByDescending"
                or "Prepend" or "Reverse" or "Select" or "SelectMany" or "Single" or "SingleOrDefault" or "Skip"
                or "SkipWhile" or "Sum" or "Take" or "TakeWhile" or "ThenBy" or "ThenByDescending" or "ToArray"
                or "ToDictionary" or "ToHashSet" or "ToList" or "ToLookup" or "Union" or "Where" or "Zip" => true,
            _ => false
        };

    /// <summary>Returns whether the terminal method has an overload that accepts a predicate.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for terminal calls with predicate overloads.</returns>
    private static bool IsPredicateTerminalName(string name)
        => name is "Any" or "Count" or "First" or "FirstOrDefault" or "Last" or "LastOrDefault"
            or "Single" or "SingleOrDefault";

    /// <summary>Returns whether the hot-path LINQ rule is enabled for this compilation or tree.</summary>
    /// <param name="context">The syntax context.</param>
    /// <returns><see langword="true"/> when the opt-in rule is enabled.</returns>
    private static bool IsAvoidLinqOnHotPathEnabled(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        return options.TryGetValue(AvoidLinqOnHotPathKey, out var value)
            && IsTrue(value);
    }

    /// <summary>Returns whether an editorconfig value is truthy.</summary>
    /// <param name="value">The option value.</param>
    /// <returns><see langword="true"/> for common truthy values.</returns>
    private static bool IsTrue(string value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
}
