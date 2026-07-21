// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared reading of a flags enum's shape: whether the type carries <c>[System.Flags]</c>, and each member's
/// constant value as the bit pattern it actually stores. The rules that reason about flag combinations
/// (the missing zero member, a literal that hides a combination) read the same facts, so they live here.
/// </summary>
internal static class EnumFlagValues
{
    /// <summary>The unqualified name of the attribute that promises an enum's members combine.</summary>
    private const string FlagsAttributeName = "FlagsAttribute";

    /// <summary>Returns whether a type carries the <c>[System.Flags]</c> attribute.</summary>
    /// <param name="type">The enum to test.</param>
    /// <returns><see langword="true"/> when the type promises its members combine with bitwise or.</returns>
    public static bool HasFlagsAttribute(INamedTypeSymbol type)
    {
        var attributes = type.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass is { Name: FlagsAttributeName } attribute
                && attribute.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads an enum member's constant value as a bit pattern.</summary>
    /// <param name="member">The candidate member.</param>
    /// <param name="value">The member's value, reinterpreted as bits.</param>
    /// <returns><see langword="true"/> when the member is an enum member with a constant value.</returns>
    /// <remarks>
    /// The underlying type can be any integral type, signed or not; every value is read as the bit pattern it
    /// stores, so a negative member on a signed enum is measured on its bits rather than its arithmetic value.
    /// </remarks>
    public static bool TryGetValue(ISymbol member, out ulong value)
    {
        if (member is not IFieldSymbol { HasConstantValue: true, ConstantValue: { } constant })
        {
            value = 0;
            return false;
        }

        value = constant is ulong bits ? bits : unchecked((ulong)Convert.ToInt64(constant, CultureInfo.InvariantCulture));
        return true;
    }

    /// <summary>Returns whether a value owns exactly one bit.</summary>
    /// <param name="value">The member's value.</param>
    /// <returns><see langword="true"/> for a power of two.</returns>
    public static bool IsSingleBit(ulong value) => value != 0 && (value & (value - 1)) == 0;
}
