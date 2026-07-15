// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Resolves whether an invocation binds to an in-memory <c>System.Linq.Enumerable</c> operator,
/// shared by the LINQ chain and usage rules (PSH1100-PSH1102, PSH1107-PSH1111). A string method
/// with the same name as a LINQ operator is rejected before the binding is inspected, so the clean
/// path stays cheap.
/// </summary>
internal static class EnumerableInvocationHelper
{
    /// <summary>Returns whether a named type is <c>System.Linq.Enumerable</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for <c>System.Linq.Enumerable</c>.</returns>
    public static bool IsSystemLinqEnumerable(INamedTypeSymbol? type)
        => type?.Name == "Enumerable"
            && type.ContainingNamespace?.Name == "Linq"
            && type.ContainingNamespace.ContainingNamespace?.Name == "System"
            && type.ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;

    /// <summary>Binds an invocation and returns its method when it targets <c>System.Linq.Enumerable</c>.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="method">The bound method symbol when the call is an in-memory LINQ operator.</param>
    /// <returns><see langword="true"/> when the target is an in-memory LINQ method.</returns>
    public static bool TryGetEnumerableMethod(
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

        var original = bound.ReducedFrom ?? bound;
        if (!IsSystemLinqEnumerable(original.ContainingType))
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
    public static bool IsEnumerableInvocation(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
        => TryGetEnumerableMethod(invocation, model, cancellationToken, out _);
}
