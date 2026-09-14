// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Compares a bound method with the other overloads in its method group, shared by the rules that
/// suggest passing a value to a sibling overload directly instead of converting it first (PSH1211,
/// PSH1212, PSH1217).
/// </summary>
internal static class SiblingOverloads
{
    /// <summary>Returns whether a member is another non-generic overload with the same staticness and parameter count as a method.</summary>
    /// <param name="member">A member of the method's containing type sharing its name.</param>
    /// <param name="method">The bound method.</param>
    /// <param name="sibling">The member as a method, when it is such an overload.</param>
    /// <returns><see langword="true"/> when the member is a same-shape sibling overload.</returns>
    internal static bool IsSameShapeSibling(ISymbol member, IMethodSymbol method, [NotNullWhen(true)] out IMethodSymbol? sibling)
    {
        if (member is IMethodSymbol candidate
            && !SymbolEqualityComparer.Default.Equals(candidate, method)
            && !candidate.IsGenericMethod
            && candidate.IsStatic == method.IsStatic
            && candidate.Parameters.Length == method.Parameters.Length)
        {
            sibling = candidate;
            return true;
        }

        sibling = null;
        return false;
    }

    /// <summary>Returns whether two methods with the same parameter count agree on every parameter type except one position.</summary>
    /// <param name="candidate">The overload being compared; its parameter count must equal the method's.</param>
    /// <param name="method">The bound method.</param>
    /// <param name="index">The position whose type may differ.</param>
    /// <returns><see langword="true"/> when every other parameter type is identical.</returns>
    internal static bool ParameterTypesMatchExcept(IMethodSymbol candidate, IMethodSymbol method, int index)
    {
        for (var i = 0; i < candidate.Parameters.Length; i++)
        {
            if (i != index && !SymbolEqualityComparer.Default.Equals(candidate.Parameters[i].Type, method.Parameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }
}
