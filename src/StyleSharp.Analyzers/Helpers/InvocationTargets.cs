// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Confirms, by binding, which type declares the method an invocation calls.</summary>
internal static class InvocationTargets
{
    /// <summary>Returns whether an invocation binds to a method declared on a type.</summary>
    /// <param name="model">The semantic model for the invocation's tree.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="type">The type expected to declare the method.</param>
    /// <param name="cancellationToken">A token that cancels binding.</param>
    /// <returns><see langword="true"/> when the call resolves to a method whose containing type is <paramref name="type"/>.</returns>
    internal static bool IsMethodOf(SemanticModel model, InvocationExpressionSyntax invocation, INamedTypeSymbol type, CancellationToken cancellationToken) =>
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, type);
}
