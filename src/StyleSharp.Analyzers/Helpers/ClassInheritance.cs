// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads whether a class inherits members from a base class of its own rather than straight from <see cref="object"/>.</summary>
internal static class ClassInheritance
{
    /// <summary>Returns whether a type is a class whose base class is something other than <c>object</c>.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for a class with a base class that can declare members of its own.</returns>
    internal static bool HasNonObjectBase(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class && type.BaseType is { SpecialType: not SpecialType.System_Object };
}
