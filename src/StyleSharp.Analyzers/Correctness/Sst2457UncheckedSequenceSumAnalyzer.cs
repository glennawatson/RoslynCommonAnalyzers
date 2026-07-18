// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an integral sequence <c>Sum</c> call lexically wrapped in <c>unchecked</c> (SST2457). The
/// wrapper only affects arithmetic written inside it, never the checked accumulation inside the
/// operator, so the call still throws on overflow — the wrapper documents a wraparound the code does
/// not deliver.
/// </summary>
/// <remarks>
/// <para>
/// Only the int and long overloads (and their nullable and selector forms) are reported: those are
/// the ones that accumulate with checked arithmetic. The float, double, and decimal overloads are
/// left alone — floating-point addition never throws on overflow, and decimal arithmetic throws with
/// or without a wrapper, so neither is a claim of wraparound gone wrong in the same way. A call whose
/// nearest wrapper is <c>checked</c> is not reported either: that wrapper claims nothing about
/// wrapping around, even when an <c>unchecked</c> block encloses it further out.
/// </para>
/// <para>
/// The clean path binds nothing. The invoked name must be <c>Sum</c> and an enclosing
/// <c>unchecked</c> construct must be found — both on syntax, walking no further up than the
/// containing member — before the invocation is bound to confirm it is the sequence operator.
/// <c>System.Linq.Enumerable</c> itself is resolved lazily, on the first candidate only, so a
/// compilation with no <c>Sum</c> inside <c>unchecked</c> never pays for the lookup.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2457UncheckedSequenceSumAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked name the syntax gate accepts.</summary>
    private const string SumMethodName = "Sum";

    /// <summary>The metadata name of the LINQ extension-method host type.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.UncheckedSequenceSum);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var enumerableType = new Lazy<INamedTypeSymbol?>(
                () => compilation.GetTypeByMetadataName(EnumerableMetadataName),
                LazyThreadSafetyMode.ExecutionAndPublication);

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, enumerableType),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports one invocation when it is an integral sequence Sum wrapped in unchecked.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="enumerableType">The lazily resolved <c>System.Linq.Enumerable</c> type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, Lazy<INamedTypeSymbol?> enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count > 2
            || GetInvokedName(invocation.Expression) != SumMethodName
            || !IsInsideUnchecked(invocation)
            || enumerableType.Value is not { } enumerable
            || !IsIntegralEnumerableSum(context, invocation, enumerable))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.UncheckedSequenceSum,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Gets the simple name an invocation calls.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns>The invoked name, or <see langword="null"/> for callee shapes that cannot be a Sum call.</returns>
    private static string? GetInvokedName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether the nearest enclosing overflow-context wrapper is <c>unchecked</c>.</summary>
    /// <param name="invocation">The Sum invocation.</param>
    /// <returns><see langword="true"/> when an <c>unchecked</c> expression or block encloses the call.</returns>
    /// <remarks>
    /// The nearest wrapper wins: a <c>checked</c> construct between the call and an outer
    /// <c>unchecked</c> block withdraws the wraparound claim, so nothing is reported. The walk stops
    /// at the containing member, because an overflow context never crosses a member boundary.
    /// </remarks>
    private static bool IsInsideUnchecked(InvocationExpressionSyntax invocation)
    {
        for (SyntaxNode? node = invocation.Parent; node is not null; node = node.Parent)
        {
            switch (node.RawKind)
            {
                case (int)SyntaxKind.UncheckedExpression:
                case (int)SyntaxKind.UncheckedStatement:
                    return true;
                case (int)SyntaxKind.CheckedExpression:
                case (int)SyntaxKind.CheckedStatement:
                    return false;
            }

            if (node is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether an invocation binds to an int or long overload of the sequence Sum operator.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    /// <returns><see langword="true"/> when the call is a Sum overload that accumulates with checked arithmetic.</returns>
    private static bool IsIntegralEnumerableSum(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType)
        => context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals((method.ReducedFrom ?? method).ContainingType, enumerableType)
            && IsIntegralSumResult(method.ReturnType);

    /// <summary>Returns whether a Sum overload's result type is one the operator accumulates with checked arithmetic.</summary>
    /// <param name="returnType">The bound Sum overload's return type.</param>
    /// <returns><see langword="true"/> for int and long results, plain or nullable.</returns>
    private static bool IsIntegralSumResult(ITypeSymbol returnType)
    {
        if (returnType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            returnType = nullable.TypeArguments[0];
        }

        return returnType.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64;
    }
}
