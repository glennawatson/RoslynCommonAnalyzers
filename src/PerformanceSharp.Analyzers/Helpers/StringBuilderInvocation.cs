// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Binding checks for <c>System.Text.StringBuilder</c> calls, shared by the rules that split the string
/// argument handed to an append (PSH1203, PSH1214).
/// </summary>
internal static class StringBuilderInvocation
{
    /// <summary>Returns whether an invocation binds to a builder instance method whose only parameter is a string.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="builderType">The compilation's string builder type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for the <c>Append(string)</c> and <c>AppendLine(string)</c> overloads the syntax gate named.</returns>
    internal static bool BindsToStringOverload(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol builderType,
        CancellationToken cancellationToken) =>
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsStatic: false, Parameters: [{ Type.SpecialType: SpecialType.System_String }] } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType);
}
