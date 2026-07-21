// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1420 descriptor.</summary>
internal static partial class ApiSelectionRules
{
    /// <summary>PSH1420 — a per-invocation function class keeps a heavyweight client in an instance field.</summary>
    public static readonly DiagnosticDescriptor ShareClientAcrossInvocations = Create(
        "PSH1420",
        "Do not hold a per-invocation client in a function class field",
        "The isolated worker constructs this function class on every invocation, so this per-instance '{0}' cannot pool its connections across calls; {1}",
        ShareClientAcrossInvocationsDescription);

    /// <summary>The PSH1420 rule description.</summary>
    private const string ShareClientAcrossInvocationsDescription =
        "The isolated worker constructs a function class once per invocation, so anything it keeps in an instance field lives "
        + "for a single call. A heavyweight client held there is therefore built and thrown away on every request: an "
        + "'HttpClient' opens a fresh connection pool and abandons its sockets to the operating system's timed-wait state when "
        + "the instance is collected, which leaks a port per call and drains the ephemeral port range under load; the cloud "
        + "service clients ('BlobServiceClient', 'ServiceBusClient', 'CosmosClient', 'SecretClient', and their siblings) rebuild "
        + "a transport pipeline, authentication state, and metadata caches each time. All of these types are thread-safe and "
        + "designed to be cached for the lifetime of the process, so the fix is to keep one shared instance: hold it in a "
        + "'static' field, or register it as a singleton and inject it; for 'HttpClient' specifically, inject an "
        + "'IHttpClientFactory' and create clients from it so the underlying handlers rotate. An instance field or instance "
        + "auto-property whose type is one of these clients is reported, but only in a class that declares at least one method "
        + "carrying the worker's function attribute. A 'static' field is never reported, because it is already the shared "
        + "instance the fix asks for. The whole rule stays silent unless both the worker function attribute and 'HttpClient' "
        + "resolve in the compilation, so a project that is not an isolated worker pays nothing.";
}
