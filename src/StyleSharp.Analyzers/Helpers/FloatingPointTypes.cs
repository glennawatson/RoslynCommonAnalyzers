// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Classifies the two binary floating-point types, <see cref="float"/> and <see cref="double"/>, whose
/// arithmetic rounds and whose <c>NaN</c> value compares false against everything — including itself.
/// </summary>
/// <remarks>
/// <c>decimal</c> is excluded on purpose. It is a base-10 type whose arithmetic is exact for the values it
/// can represent, so <c>price == total</c> means what it says and must never be reported.
/// </remarks>
internal static class FloatingPointTypes
{
    /// <summary>The C# keyword for <see cref="float"/>, used in messages and by the code fix.</summary>
    public const string SingleKeyword = "float";

    /// <summary>The C# keyword for <see cref="double"/>, used in messages and by the code fix.</summary>
    public const string DoubleKeyword = "double";

    /// <summary>Returns whether a type is <see cref="float"/> or <see cref="double"/>, or the nullable form of either.</summary>
    /// <param name="type">The type to classify, which may be unresolved.</param>
    /// <returns><see langword="true"/> for a binary floating-point type.</returns>
    public static bool IsBinaryFloatingPoint(ITypeSymbol? type) => TryGetKeyword(type, out _, out _);

    /// <summary>Reads the C# keyword of a binary floating-point type, seeing through <see cref="Nullable{T}"/>.</summary>
    /// <param name="type">The type to classify, which may be unresolved.</param>
    /// <param name="keyword">The <c>float</c> or <c>double</c> keyword, or an empty string.</param>
    /// <param name="isNullable">Whether the type was the nullable form, which no <c>IsNaN</c> overload accepts.</param>
    /// <returns><see langword="true"/> when the type is a binary floating-point type.</returns>
    public static bool TryGetKeyword(ITypeSymbol? type, out string keyword, out bool isNullable)
    {
        keyword = string.Empty;
        isNullable = false;
        if (type is null)
        {
            return false;
        }

        var underlying = type;
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && type is INamedTypeSymbol { TypeArguments.Length: 1 } nullable)
        {
            underlying = nullable.TypeArguments[0];
            isNullable = true;
        }

        keyword = underlying.SpecialType switch
        {
            SpecialType.System_Double => DoubleKeyword,
            SpecialType.System_Single => SingleKeyword,
            _ => string.Empty,
        };

        return keyword.Length > 0;
    }
}
