// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The exception types that state a member is deliberately absent rather than failing at runtime, which
/// SST1485 therefore allows even where a member must not throw.
/// </summary>
/// <param name="NotImplemented">The <see cref="System.NotImplementedException"/> symbol, when the compilation has one.</param>
/// <param name="NotSupported">The <see cref="System.NotSupportedException"/> symbol, when the compilation has one.</param>
/// <remarks>
/// <c>throw new NotImplementedException();</c> in a generated <c>Equals</c> is a placeholder, and
/// <c>NotSupportedException</c> is how an interface member says it does not apply to this implementation.
/// Both are contracts, not failures, and reporting them would be arguing with the language's own idioms.
/// </remarks>
internal readonly record struct AllowedThrowTypes(INamedTypeSymbol? NotImplemented, INamedTypeSymbol? NotSupported)
{
    /// <summary>The simple name of <see cref="System.NotImplementedException"/>.</summary>
    public const string NotImplementedName = "NotImplementedException";

    /// <summary>The simple name of <see cref="System.NotSupportedException"/>.</summary>
    public const string NotSupportedName = "NotSupportedException";

    /// <summary>Resolves the well-known types once per compilation.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>The resolved symbols; either may be <see langword="null"/>.</returns>
    public static AllowedThrowTypes Create(Compilation compilation) => new(
        compilation.GetTypeByMetadataName("System." + NotImplementedName),
        compilation.GetTypeByMetadataName("System." + NotSupportedName));

    /// <summary>Returns whether a thrown type is one of the allowed types, or derives from one.</summary>
    /// <param name="type">The thrown expression's type.</param>
    /// <returns><see langword="true"/> when the throw marks the member as deliberately absent.</returns>
    public bool Contains(ITypeSymbol? type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, NotImplemented)
                || SymbolEqualityComparer.Default.Equals(current, NotSupported))
            {
                return true;
            }
        }

        return false;
    }
}
