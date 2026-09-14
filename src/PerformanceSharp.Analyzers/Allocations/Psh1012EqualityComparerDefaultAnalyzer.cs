// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>x.Equals(y)</c> and <c>object.Equals(x, y)</c> where the operands are values of a
/// type parameter that carries no equality the compiler can bind directly (PSH1012). Such
/// calls resolve to <c>Object.Equals(object)</c>, boxing the argument — and both operands for
/// the static overload — on every value-type instantiation. <c>EqualityComparer&lt;T&gt;.Default.Equals</c>
/// compares without boxing and the JIT devirtualizes it. Reference-constrained type
/// parameters never box and are not reported. Gated on <c>EqualityComparer&lt;T&gt;</c>
/// existing in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1012EqualityComparerDefaultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string EqualsMethodName = "Equals";

    /// <summary>The argument count of the static object.Equals overload.</summary>
    private const int StaticEqualsArgumentCount = 2;

    /// <summary>The metadata name of the comparer type the fix moves to.</summary>
    private const string EqualityComparerMetadataName = "System.Collections.Generic.EqualityComparer`1";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AllocationRules.UseEqualityComparerDefault);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => LazyCompilationProbe.Create(compilation, HasEqualityComparer),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns the operands and type parameter of a boxing equality call, or <see langword="null"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to classify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The two compared expressions and their boxing-prone type parameter.</returns>
    internal static BoxingComparison? TryGetBoxingComparison(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count switch
        {
            1 when invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: EqualsMethodName } access
                => TryGetInstanceComparison(model, access.Expression, arguments[0].Expression, invocation, cancellationToken),
            StaticEqualsArgumentCount => TryGetStaticComparison(model, invocation, cancellationToken),
            _ => null,
        };
    }

    /// <summary>Classifies an instance <c>x.Equals(y)</c> call on a type parameter receiver.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="receiver">The receiver expression.</param>
    /// <param name="argument">The single argument expression.</param>
    /// <param name="invocation">The whole invocation, for symbol binding.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The comparison parts, or <see langword="null"/> when the call does not box.</returns>
    private static BoxingComparison? TryGetInstanceComparison(
        SemanticModel model,
        ExpressionSyntax receiver,
        ExpressionSyntax argument,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken) => model.GetTypeInfo(receiver, cancellationToken).Type is not ITypeParameterSymbol typeParameter
            || typeParameter.IsReferenceType
            || !SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(argument, cancellationToken).Type, typeParameter)
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { Parameters.Length: 1 } method
            || method.ContainingType.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType)
            ? null
            : new BoxingComparison(receiver, argument, typeParameter);

    /// <summary>Classifies a static <c>object.Equals(x, y)</c> call over type parameter operands.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to classify.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The comparison parts, or <see langword="null"/> when the call does not box.</returns>
    private static BoxingComparison? TryGetStaticComparison(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        if (MemberReferenceName.Of(invocation.Expression) != EqualsMethodName)
        {
            return null;
        }

        var left = invocation.ArgumentList.Arguments[0].Expression;
        var right = invocation.ArgumentList.Arguments[1].Expression;
        return model.GetTypeInfo(left, cancellationToken).Type is not ITypeParameterSymbol typeParameter
            || typeParameter.IsReferenceType
            || !SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(right, cancellationToken).Type, typeParameter)
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || method.ContainingType.SpecialType != SpecialType.System_Object
            ? null
            : new BoxingComparison(left, right, typeParameter);
    }

    /// <summary>Reports PSH1012 for an equality call that boxes its type parameter operands.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="comparerType">The compilation's deferred comparer availability check.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyCompilationProbe comparerType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count is not (1 or StaticEqualsArgumentCount)
            || MemberReferenceName.Of(invocation.Expression) != EqualsMethodName
            || (invocation.ArgumentList.Arguments.Count == 1 && invocation.Expression is not MemberAccessExpressionSyntax)
            || !comparerType.Get()
            || TryGetBoxingComparison(context.SemanticModel, invocation, context.CancellationToken) is not { } comparison)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UseEqualityComparerDefault,
            invocation.SyntaxTree,
            invocation.Span,
            comparison.TypeParameter.Name));
    }

    /// <summary>Returns whether the compilation exposes the comparer type the fix moves to.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when the comparer type resolves.</returns>
    private static bool HasEqualityComparer(Compilation compilation) =>
        compilation.GetTypeByMetadataName(EqualityComparerMetadataName) is not null;
}
