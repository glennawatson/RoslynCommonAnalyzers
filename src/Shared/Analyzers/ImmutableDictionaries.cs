// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Factory for the empty <see cref="ImmutableDictionary{TKey, TValue}"/> that analyzers
/// hand to <c>Diagnostic.Create</c> as a property bag.
/// </summary>
/// <remarks>
/// The sibling of <see cref="ImmutableArrays"/>, bridging the same multi-Roslyn split one
/// collection type further along. <see cref="ImmutableDictionary{TKey, TValue}"/> only gained
/// a collection builder in the System.Collections.Immutable that the roslyn5.9 slot
/// references, so a literal <c>[]</c> fails to compile on every earlier slot (CS1729) while
/// the SDK's own style rules require it on roslyn5.9. Funnelling the site through a method
/// call keeps that split in one place and leaves neither rule anything to object to.
/// </remarks>
internal static class ImmutableDictionaries
{
    /// <summary>Gets the empty immutable dictionary for the given key and value types.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>The shared empty instance; no allocation occurs.</returns>
    public static ImmutableDictionary<TKey, TValue> Empty<TKey, TValue>()
        where TKey : notnull
#if ROSLYN_5_9_OR_GREATER
        => [];
#else
        => ImmutableDictionary<TKey, TValue>.Empty;
#endif
}
