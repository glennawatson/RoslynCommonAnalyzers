// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Tiny allocation-light factories for the single-element <see cref="ImmutableArray{T}"/>
/// values analyzers and code fixes expose (SupportedDiagnostics, FixableDiagnosticIds).
/// </summary>
/// <remarks>
/// This exists to bridge a multi-Roslyn split: the roslyn4.8 floor ships an
/// <see cref="ImmutableArray{T}"/> that predates collection-expression support
/// (CS9210), so a literal <c>[item]</c> will not compile there; on roslyn4.14+
/// the the rule style rule conversely requires the collection expression over
/// <c>ImmutableArray.Create</c>. Funnelling every site through this one helper
/// keeps the <c>#if</c> in a single place and leaves call sites as plain method
/// calls that neither rule objects to.
/// </remarks>
internal static class ImmutableArrays
{
    /// <summary>Creates a single-element immutable array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="item">The only element.</param>
    /// <returns>An immutable array containing <paramref name="item"/>.</returns>
    public static ImmutableArray<T> Of<T>(T item)
#if ROSLYN_4_14_OR_GREATER
        => [item];
#else
        => ImmutableArray.Create(item);
#endif

    /// <summary>Creates an immutable array from the supplied elements.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The elements.</param>
    /// <returns>An immutable array containing <paramref name="items"/>.</returns>
    public static ImmutableArray<T> Of<T>(params T[] items)
#if ROSLYN_4_14_OR_GREATER
        => [.. items];
#else
        => ImmutableArray.Create(items);
#endif
}
