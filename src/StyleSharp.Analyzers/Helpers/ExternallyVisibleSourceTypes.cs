// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Selects the types a public-surface rule asks something of: classes and structs declared in this compilation,
/// visible outside the assembly, and able to have instances.
/// </summary>
internal static class ExternallyVisibleSourceTypes
{
    /// <summary>Returns whether a type is a non-static class or struct, visible outside the assembly and declared in source.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for an instantiable, externally visible source type.</returns>
    internal static bool IsInstanceClassOrStruct(INamedTypeSymbol type) =>
        type.TypeKind is TypeKind.Class or TypeKind.Struct
            && !type.IsStatic
            && SymbolVisibility.IsExternallyVisible(type)
            && !type.Locations.IsEmpty
            && type.Locations[0].IsInSource;
}
