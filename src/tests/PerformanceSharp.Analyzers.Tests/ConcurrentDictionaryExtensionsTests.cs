// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the stateful cache factory compatibility overload.</summary>
public class ConcurrentDictionaryExtensionsTests
{
    /// <summary>Verifies a miss passes both the key and explicit state to the factory.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactoryReceivesKeyAndStateAsync()
    {
        var cache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var value = ConcurrentDictionaryExtensions.GetOrAdd(cache, "key", static (key, state) => key + state, "-state");

        await Assert.That(value).IsEqualTo("key-state");
        await Assert.That(cache["key"]).IsEqualTo(value);
    }

    /// <summary>Verifies a null resolution is cached and later hits do not invoke the factory.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CachedNullDoesNotInvokeFactoryAsync()
    {
        const string MissingKey = "missing";
        var cache = new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);
        var first = ConcurrentDictionaryExtensions.GetOrAdd(cache, MissingKey, static (_, _) => (string?)null, 0);
        var second = ConcurrentDictionaryExtensions.GetOrAdd(cache, MissingKey, static (_, _) => throw new InvalidOperationException("A cached null must be a hit."), 0);

        await Assert.That(first).IsNull();
        await Assert.That(second).IsNull();
        await Assert.That(cache.ContainsKey(MissingKey)).IsTrue();
    }

    /// <summary>Verifies a value published during factory execution wins over the computed value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompetingPublicationWinsOverComputedValueAsync()
    {
        var cache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var value = ConcurrentDictionaryExtensions.GetOrAdd(
            cache,
            "key",
            static (key, state) =>
            {
                _ = state.TryAdd(key, "published");
                return "computed";
            },
            cache);

        await Assert.That(value).IsEqualTo("published");
        await Assert.That(cache["key"]).IsEqualTo(value);
    }
}
