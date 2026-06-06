// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System;

/// <summary>
/// Represents a range with start and end indices. Polyfilled on the netstandard2.0
/// analyzer assemblies so span slices can use the allocation-free range syntax.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal readonly struct Range : IEquatable<Range>
{
    /// <summary>Initializes a new instance of the <see cref="Range"/> struct.</summary>
    /// <param name="start">The inclusive start index.</param>
    /// <param name="end">The exclusive end index.</param>
    public Range(Index start, Index end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets a range containing all elements.</summary>
    public static Range All => new(new(0), new(0, fromEnd: true));

    /// <summary>Gets the inclusive start index.</summary>
    public Index Start { get; }

    /// <summary>Gets the exclusive end index.</summary>
    public Index End { get; }

    /// <summary>Creates a range starting at <paramref name="start"/> and continuing to the end.</summary>
    /// <param name="start">The inclusive start index.</param>
    /// <returns>The range.</returns>
    public static Range StartAt(Index start) => new(start, new(0, fromEnd: true));

    /// <summary>Creates a range from the beginning to <paramref name="end"/>.</summary>
    /// <param name="end">The exclusive end index.</param>
    /// <returns>The range.</returns>
    public static Range EndAt(Index end) => new(new(0), end);

    /// <summary>Calculates the offset and length for a collection of the given length.</summary>
    /// <param name="length">The collection length.</param>
    /// <returns>The offset and length represented by this range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is outside the collection.</exception>
    public (int Offset, int Length) GetOffsetAndLength(int length)
    {
        var start = Start.GetOffset(length);
        var end = End.GetOffset(length);
        if ((uint)end > (uint)length || (uint)start > (uint)end)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return (start, end - start);
    }

    /// <inheritdoc/>
    public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Range other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (Start.GetHashCode() * 397) ^ End.GetHashCode();
}

#else
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(System.Range))]
#endif
