// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Resolves whether an invocation binds to an in-memory <c>System.Linq.Enumerable</c> operator,
/// shared by the LINQ chain and usage rules (PSH1100-PSH1102, PSH1107-PSH1111). A string method
/// with the same name as a LINQ operator is rejected before the binding is inspected, so the clean
/// path stays cheap.
/// </summary>
internal static class EnumerableInvocationHelper
{
    /// <summary>The unreduced parameter count of an extension that takes only its source.</summary>
    private const int SourceOnlyParameterCount = 1;

    /// <summary>Returns whether an invocation binds, in extension form, to a method declared on a resolved LINQ type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The LINQ extension class resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a reduced extension declared on <paramref name="enumerableType"/>.</returns>
    internal static bool IsReducedExtensionOn(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        CancellationToken cancellationToken) =>
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: { } reduced }
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);

    /// <summary>Returns whether an invocation binds, in extension form, to a resolved LINQ type's method with a given unreduced arity.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The LINQ extension class resolved for the compilation.</param>
    /// <param name="parameterCount">The expected parameter count of the unreduced method, source included.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a reduced extension of that arity declared on <paramref name="enumerableType"/>.</returns>
    internal static bool IsReducedExtensionOn(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        int parameterCount,
        CancellationToken cancellationToken) =>
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: { } reduced }
            && reduced.Parameters.Length == parameterCount
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);

    /// <summary>Returns whether an invocation binds, in extension form, to a resolved LINQ type's method whose only parameter is the source.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The LINQ extension class resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a reduced source-only extension declared on <paramref name="enumerableType"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSourceOnlyExtensionOn(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        CancellationToken cancellationToken) =>
        IsReducedExtensionOn(model, invocation, enumerableType, SourceOnlyParameterCount, cancellationToken);

    /// <summary>Returns whether a named type is <c>System.Linq.Enumerable</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for <c>System.Linq.Enumerable</c>.</returns>
    internal static bool IsSystemLinqEnumerable(INamedTypeSymbol? type) =>
        type?.Name == "Enumerable"
            && type.ContainingNamespace?.Name == "Linq"
            && type.ContainingNamespace.ContainingNamespace?.Name == "System"
            && type.ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;

    /// <summary>Binds an invocation and returns its method when it targets <c>System.Linq.Enumerable</c>.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="method">The bound method symbol when the call is an in-memory LINQ operator.</param>
    /// <returns><see langword="true"/> when the target is an in-memory LINQ method.</returns>
    internal static bool TryGetEnumerableMethod(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out IMethodSymbol method)
    {
        method = null!;
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol bound)
        {
            return false;
        }

        if (bound.ContainingType?.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (!IsSystemLinqEnumerable((bound.ReducedFrom ?? bound).ContainingType))
        {
            return false;
        }

        method = bound;
        return true;
    }

    /// <summary>Returns whether the invocation resolves to <see cref="System.Linq.Enumerable"/>.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the target is an in-memory LINQ method.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsEnumerableInvocation(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken) =>
        TryGetEnumerableMethod(invocation, model, cancellationToken, out _);
}
