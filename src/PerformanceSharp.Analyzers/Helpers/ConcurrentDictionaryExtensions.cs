// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>Supplies the stateful cache factory overload missing from .NET Standard 2.0.</summary>
internal static class ConcurrentDictionaryExtensions
{
    /// <summary>Adds explicit factory state to concurrent cache lookups.</summary>
    /// <typeparam name="TKey">The cache key type.</typeparam>
    /// <typeparam name="TValue">The cached value type.</typeparam>
    /// <param name="dictionary">The cache to query.</param>
    extension<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        /// <summary>Resolves cache misses without capturing factory state in a delegate.</summary>
        /// <typeparam name="TArg">The factory state type.</typeparam>
        /// <param name="key">The key to resolve.</param>
        /// <param name="valueFactory">A non-capturing factory that tolerates concurrent duplicate calls.</param>
        /// <param name="factoryArgument">The state passed to the factory on a miss.</param>
        /// <returns>The cached value, including a null result, or the value that wins publication.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TValue GetOrAdd<TArg>(TKey key, Func<TKey, TArg, TValue> valueFactory, TArg factoryArgument) =>
            dictionary.TryGetValue(key, out var value) ? value : dictionary.GetOrAdd(key, valueFactory(key, factoryArgument));
    }
}
