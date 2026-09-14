// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads whether a method overrides a member that <see cref="object"/> itself declares.</summary>
internal static class ObjectMemberOverrides
{
    /// <summary>Returns whether a method overrides a member that is ultimately declared on <c>object</c>.</summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for an override of <c>ToString</c>, <c>Equals</c>, <c>GetHashCode</c>, and the like, however deep the chain.</returns>
    internal static bool IsOverrideOfObjectMember(IMethodSymbol method)
    {
        if (!method.IsOverride)
        {
            return false;
        }

        var root = method;
        while (root.OverriddenMethod is { } overridden)
        {
            root = overridden;
        }

        return root.ContainingType?.SpecialType == SpecialType.System_Object;
    }
}
