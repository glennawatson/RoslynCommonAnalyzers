// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Estimates the size in bytes a value type occupies, for the size gate on PSH1007.
/// </summary>
/// <remarks>
/// Roslyn does not expose a type's runtime layout, so the size is summed from the declared instance
/// fields with sequential-layout padding — the layout C# emits for a struct by default. It is an
/// estimate, not a guarantee: the runtime may pack an auto-layout struct more tightly. The estimate
/// therefore only ever runs slightly high, which the rule compensates for with a threshold well above
/// the register-passing boundary. An unresolvable type reports <see cref="Unknown"/> and is never
/// reported on, so a type this cannot measure is silently left alone rather than guessed at.
/// </remarks>
internal static class StructSizeEstimator
{
    /// <summary>The size of a reference or a native integer on the platforms this targets.</summary>
    public const int PointerSize = 8;

    /// <summary>The result for a type whose size cannot be determined.</summary>
    public const int Unknown = -1;

    /// <summary>The deepest nesting the estimator will walk before giving up.</summary>
    private const int MaximumDepth = 8;

    /// <summary>The size of a bool, byte or sbyte, and the size an empty struct still occupies.</summary>
    private const int OneByte = 1;

    /// <summary>The size of a char, short or ushort.</summary>
    private const int TwoBytes = 2;

    /// <summary>The size of an int, uint or float.</summary>
    private const int FourBytes = 4;

    /// <summary>The size of a long, ulong or double.</summary>
    private const int EightBytes = 8;

    /// <summary>The size of a decimal.</summary>
    private const int DecimalSize = 16;

    /// <summary>Estimates a type's size, memoizing the result for the compilation's lifetime.</summary>
    /// <param name="type">The type to measure.</param>
    /// <param name="cache">The per-compilation size cache.</param>
    /// <returns>The estimated size in bytes, or <see cref="Unknown"/>.</returns>
    public static int Estimate(ITypeSymbol type, ConcurrentDictionary<ITypeSymbol, int> cache)
    {
        if (cache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var size = Measure(type, 0, out _);
        cache.TryAdd(type, size);
        return size;
    }

    /// <summary>Measures a type's size and alignment.</summary>
    /// <param name="type">The type to measure.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <param name="alignment">The type's alignment.</param>
    /// <returns>The size in bytes, or <see cref="Unknown"/>.</returns>
    private static int Measure(ITypeSymbol type, int depth, out int alignment)
    {
        alignment = 1;
        if (depth > MaximumDepth)
        {
            return Unknown;
        }

        if (type.IsReferenceType || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
        {
            alignment = PointerSize;
            return PointerSize;
        }

        var primitive = MeasurePrimitive(type.SpecialType);
        if (primitive != Unknown)
        {
            alignment = Math.Min(primitive, PointerSize);
            return primitive;
        }

        if (type is not INamedTypeSymbol named)
        {
            return Unknown;
        }

        if (named.TypeKind == TypeKind.Enum)
        {
            return named.EnumUnderlyingType is { } underlying ? Measure(underlying, depth + 1, out alignment) : Unknown;
        }

        return named.TypeKind == TypeKind.Struct ? MeasureStruct(named, depth, out alignment) : Unknown;
    }

    /// <summary>Measures a struct by laying its instance fields out sequentially.</summary>
    /// <param name="type">The struct to measure.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <param name="alignment">The struct's alignment, which is that of its widest field.</param>
    /// <returns>The size in bytes, or <see cref="Unknown"/>.</returns>
    private static int MeasureStruct(INamedTypeSymbol type, int depth, out int alignment)
    {
        alignment = 1;
        var offset = 0;
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IFieldSymbol { IsStatic: false, IsConst: false } field)
            {
                continue;
            }

            var size = Measure(field.Type, depth + 1, out var fieldAlignment);
            if (size == Unknown)
            {
                return Unknown;
            }

            offset = RoundUp(offset, fieldAlignment) + size;
            alignment = Math.Max(alignment, fieldAlignment);
        }

        // An empty struct still occupies a byte.
        return offset == 0 ? OneByte : RoundUp(offset, alignment);
    }

    /// <summary>Gets the size of a primitive special type.</summary>
    /// <param name="specialType">The special type.</param>
    /// <returns>The size in bytes, or <see cref="Unknown"/> when the type is not primitive.</returns>
    private static int MeasurePrimitive(SpecialType specialType)
    {
        if (specialType == SpecialType.System_Decimal)
        {
            return DecimalSize;
        }

        var narrow = MeasureNarrow(specialType);
        return narrow != Unknown ? narrow : MeasureWord(specialType);
    }

    /// <summary>Gets the size of a one- or two-byte primitive.</summary>
    /// <param name="specialType">The special type.</param>
    /// <returns>The size in bytes, or <see cref="Unknown"/>.</returns>
    private static int MeasureNarrow(SpecialType specialType) => specialType switch
    {
        SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => OneByte,
        SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => TwoBytes,
        _ => Unknown,
    };

    /// <summary>Gets the size of a word-sized or wider primitive.</summary>
    /// <param name="specialType">The special type.</param>
    /// <returns>The size in bytes, or <see cref="Unknown"/>.</returns>
    private static int MeasureWord(SpecialType specialType) => specialType switch
    {
        SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => FourBytes,
        SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => EightBytes,
        SpecialType.System_IntPtr or SpecialType.System_UIntPtr => PointerSize,
        _ => Unknown,
    };

    /// <summary>Rounds an offset up to the next multiple of an alignment.</summary>
    /// <param name="offset">The current offset.</param>
    /// <param name="alignment">The alignment to round to.</param>
    /// <returns>The aligned offset.</returns>
    private static int RoundUp(int offset, int alignment)
    {
        var remainder = offset % alignment;
        return remainder == 0 ? offset : offset + alignment - remainder;
    }
}
