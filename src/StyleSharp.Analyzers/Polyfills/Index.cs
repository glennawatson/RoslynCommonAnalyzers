// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System;

/// <summary>
/// Represents a type that can be used to index a collection either from the start
/// or the end. Polyfilled on the netstandard2.0 analyzer assemblies so the source
/// can use list patterns (<c>[a, b]</c>), which the compiler lowers through
/// <see cref="GetOffset"/>.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal readonly struct Index : IEquatable<Index>
{
    /// <summary>The encoded value: non-negative for a from-start index, bitwise-complemented for a from-end index.</summary>
    private readonly int _value;

    /// <summary>Initializes a new instance of the <see cref="Index"/> struct.</summary>
    /// <param name="value">The index value. Must be non-negative.</param>
    /// <param name="fromEnd">Whether the index is counted from the end of the collection.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is negative.</exception>
    public Index(int value, bool fromEnd = false)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The value must be a non-negative integer.");
        }

        _value = fromEnd ? ~value : value;
    }

    /// <summary>Gets the index value, which is always non-negative.</summary>
    public int Value => _value < 0 ? ~_value : _value;

    /// <summary>Gets a value indicating whether the index is counted from the end of the collection.</summary>
    public bool IsFromEnd => _value < 0;

    /// <summary>Converts an integer to an <see cref="Index"/> counted from the start.</summary>
    /// <param name="value">The index value counted from the start.</param>
    /// <returns>An <see cref="Index"/> for <paramref name="value"/>.</returns>
    public static implicit operator Index(int value) => new(value);

    /// <summary>Returns whether two indices denote the same position.</summary>
    /// <param name="left">The first index.</param>
    /// <param name="right">The second index.</param>
    /// <returns><see langword="true"/> when the indices are equal.</returns>
    public static bool operator ==(Index left, Index right) => left._value == right._value;

    /// <summary>Returns whether two indices denote different positions.</summary>
    /// <param name="left">The first index.</param>
    /// <param name="right">The second index.</param>
    /// <returns><see langword="true"/> when the indices differ.</returns>
    public static bool operator !=(Index left, Index right) => left._value != right._value;

    /// <summary>Calculates the offset from the start of a collection of the given length.</summary>
    /// <param name="length">The length of the collection.</param>
    /// <returns>The zero-based offset from the start.</returns>
    public int GetOffset(int length) => IsFromEnd ? _value + length + 1 : _value;

    /// <inheritdoc/>
    public bool Equals(Index other) => _value == other._value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Index other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value;
}

#else
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(System.Index))]
#endif
