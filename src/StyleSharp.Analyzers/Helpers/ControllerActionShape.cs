// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads whether a controller method has the shape MVC action discovery routes, before any attribute is considered.</summary>
internal static class ControllerActionShape
{
    /// <summary>Returns whether a method is one action discovery can route.</summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for a public, non-static, non-abstract, non-generic, ordinary method that is not an <c>object</c> override.</returns>
    internal static bool IsRoutable(IMethodSymbol method) =>
        method.DeclaredAccessibility == Accessibility.Public
            && !method.IsStatic
            && !method.IsAbstract
            && !method.IsGenericMethod
            && method.MethodKind == MethodKind.Ordinary
            && !ObjectMemberOverrides.IsOverrideOfObjectMember(method);
}
