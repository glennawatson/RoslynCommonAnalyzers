// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags an emptiness comparison against an awaited <c>CountAsync()</c> result (PSH1126):
/// <c>await q.CountAsync() &gt; 0</c> becomes <c>await q.AnyAsync()</c>, and
/// <c>await q.CountAsync() == 0</c> becomes <c>!await q.AnyAsync()</c>. Both operand orders and
/// the zero/one literal forms are recognized.
/// <para>
/// <c>CountAsync</c> and <c>AnyAsync</c> are provider extension methods, not framework APIs, so
/// the sibling is never assumed from the name. The reported <c>CountAsync</c> call is bound, and
/// the replacement is resolved off <em>its own</em> containing type by reducing each
/// <c>AnyAsync</c> candidate against the very receiver being counted
/// (<see cref="IMethodSymbol.ReduceExtensionMethod"/>) and requiring an exact parameter-type
/// match plus a <c>Task&lt;bool&gt;</c>/<c>ValueTask&lt;bool&gt;</c> return. A provider that
/// counts but cannot answer <c>AnyAsync</c> for that receiver is therefore never reported, and
/// the rule works for any async query provider rather than one hard-coded vendor.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1126UseAnyAsyncOverCountAsyncAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The counting member name the syntax gate requires.</summary>
    internal const string CountAsyncMethodName = "CountAsync";

    /// <summary>The member name the code fix moves emptiness checks to.</summary>
    internal const string AnyAsyncMethodName = "AnyAsync";

    /// <summary>The metadata name of the generic task type a counting call must return.</summary>
    private const string TaskOfTMetadataName = "System.Threading.Tasks.Task`1";

    /// <summary>The metadata name of the generic value-task type a counting call may return.</summary>
    private const string ValueTaskOfTMetadataName = "System.Threading.Tasks.ValueTask`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseAnyAsyncOverCountAsync);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(TaskOfTMetadataName) is not { } taskOfT)
            {
                return;
            }

            var awaitables = new AwaitableTypes(taskOfT, start.Compilation.GetTypeByMetadataName(ValueTaskOfTMetadataName));
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeComparison(nodeContext, awaitables),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression,
                SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.LessThanExpression,
                SyntaxKind.LessThanOrEqualExpression);
        });
    }

    /// <summary>Classifies an emptiness-shaped awaited CountAsync() comparison, before any binding.</summary>
    /// <param name="binary">The comparison to inspect.</param>
    /// <returns>The awaited CountAsync invocation and whether the check means "has elements", or <see langword="null"/>.</returns>
    internal static (InvocationExpressionSyntax Invocation, bool HasElements)? TryGetComparisonShape(BinaryExpressionSyntax binary)
    {
        var shape = EmptinessComparisonClassifier.Classify(binary, TryGetAwaitedCount(binary.Left), TryGetAwaitedCount(binary.Right));
        return shape is { } resolved ? (resolved.Count, resolved.HasElements) : null;
    }

    /// <summary>Resolves the <c>AnyAsync</c> sibling of a bound <c>CountAsync</c> call, proving it exists for this receiver.</summary>
    /// <param name="countAsync">The bound, reduced CountAsync extension method.</param>
    /// <param name="awaitables">The awaitable types resolved for the compilation.</param>
    /// <returns>The applicable AnyAsync method, or <see langword="null"/> when the provider has none that matches.</returns>
    internal static IMethodSymbol? TryResolveAnySibling(IMethodSymbol countAsync, in AwaitableTypes awaitables)
    {
        if (countAsync.ReceiverType is not { } receiverType
            || !IsAwaitableOf(countAsync.ReturnType, SpecialType.System_Int32, awaitables))
        {
            return null;
        }

        var candidates = countAsync.ContainingType.GetMembers(AnyAsyncMethodName);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is not IMethodSymbol { IsStatic: true, IsExtensionMethod: true } candidate
                || candidate.ReduceExtensionMethod(receiverType) is not { } reduced
                || !IsAwaitableOf(reduced.ReturnType, SpecialType.System_Boolean, awaitables)
                || !ParametersMatch(countAsync.Parameters, reduced.Parameters))
            {
                continue;
            }

            return reduced;
        }

        return null;
    }

    /// <summary>Reports PSH1126 for an emptiness comparison of an awaited CountAsync() result.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="awaitables">The awaitable types resolved for the compilation.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context, AwaitableTypes awaitables)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (TryGetComparisonShape(binary) is not { } shape)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(shape.Invocation, context.CancellationToken).Symbol
                is not IMethodSymbol { IsExtensionMethod: true, Name: CountAsyncMethodName } countAsync
            || TryResolveAnySibling(countAsync, awaitables) is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseAnyAsyncOverCountAsync,
            binary.SyntaxTree,
            binary.Span,
            CountAsyncMethodName,
            AnyAsyncMethodName));
    }

    /// <summary>Returns the CountAsync invocation behind an awaited comparison operand.</summary>
    /// <param name="expression">The comparison operand.</param>
    /// <returns>The CountAsync invocation, or <see langword="null"/>.</returns>
    private static InvocationExpressionSyntax? TryGetAwaitedCount(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized.Expression;
        }

        return current is AwaitExpressionSyntax { Expression: InvocationExpressionSyntax invocation }
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == CountAsyncMethodName
            ? invocation
            : null;
    }

    /// <summary>Returns whether a return type is a task or value task carrying the given element type.</summary>
    /// <param name="type">The return type to classify.</param>
    /// <param name="element">The expected awaited result type.</param>
    /// <param name="awaitables">The awaitable types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the type awaits to the expected element type.</returns>
    private static bool IsAwaitableOf(ITypeSymbol type, SpecialType element, in AwaitableTypes awaitables)
    {
        if (type is not INamedTypeSymbol { TypeArguments.Length: 1 } named
            || named.TypeArguments[0].SpecialType != element)
        {
            return false;
        }

        var definition = named.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, awaitables.TaskOfT)
            || SymbolEqualityComparer.Default.Equals(definition, awaitables.ValueTaskOfT);
    }

    /// <summary>Returns whether two reduced parameter lists have identical types, so the same arguments bind.</summary>
    /// <param name="left">The counting call's parameters.</param>
    /// <param name="right">The candidate replacement's parameters.</param>
    /// <returns><see langword="true"/> when the lists match position for position.</returns>
    private static bool ParametersMatch(ImmutableArray<IParameterSymbol> left, ImmutableArray<IParameterSymbol> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[i].Type, right[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The awaitable types resolved once per compilation.</summary>
    /// <param name="TaskOfT">The generic task type.</param>
    /// <param name="ValueTaskOfT">The generic value-task type, when the framework has one.</param>
    internal readonly record struct AwaitableTypes(INamedTypeSymbol TaskOfT, INamedTypeSymbol? ValueTaskOfT);
}
