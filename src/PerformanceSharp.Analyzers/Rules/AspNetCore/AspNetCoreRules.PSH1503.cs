// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <content>The PSH1503 descriptor: prefer output caching over the legacy response-caching middleware.</content>
internal static partial class AspNetCoreRules
{
    /// <summary>PSH1503 — prefer output caching over the legacy response-caching middleware.</summary>
    public static readonly DiagnosticDescriptor PreferOutputCaching = CreateInfo(
        "PSH1503",
        "Prefer output caching over response caching",
        "Replace the legacy '{0}' registration with output caching's '{1}'",
        PreferOutputCachingDescription);

    /// <summary>The PSH1503 rule description.</summary>
    private const string PreferOutputCachingDescription =
        "The response-caching middleware only writes and honors HTTP cache-control headers, so it caches nothing a request's headers "
        + "forbid, skips authenticated and non-GET requests, and gives the app no key or eviction control — a cache the server cannot "
        + "actually manage. Output caching (.NET 7+) stores rendered responses on the server under keys and tags you choose, caches "
        + "requests the header-driven cache would refuse, and can be invalidated on demand. Where the output-caching API is present, "
        + "register 'AddOutputCache()' and 'UseOutputCache()' and opt endpoints in with 'CacheOutput()' instead. Reported only when "
        + "output caching is available in the referenced framework.";
}
