// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1418 descriptor.</summary>
internal static partial class ApiSelectionRules
{
    /// <summary>PSH1418 — an <c>HttpClient</c> is constructed for a single call and discarded with it.</summary>
    public static readonly DiagnosticDescriptor ReuseHttpClient = Create(
        "PSH1418",
        "Reuse an HttpClient instead of constructing one per call",
        "This 'HttpClient' is constructed for a single call and its connection pool dies with it; {0}",
        ReuseHttpClientDescription);

    /// <summary>The PSH1418 rule description.</summary>
    private const string ReuseHttpClientDescription =
        "Every 'HttpClient' owns its own connection pool. One constructed for a single request opens a fresh connection, "
        + "then abandons the socket to the operating system's timed-wait state when it is disposed — so a client created per "
        + "call leaks a port per call and drains the ephemeral port range under load, and the throughput of the whole process "
        + "collapses long before that. A single long-lived instance shares and reuses its pooled connections across every call. "
        + "Only the two shapes that prove the instance dies with the call are reported: a 'using' declaration or 'using' statement "
        + "over the construction, and a construction used directly as the receiver of the call it feeds. A construction stored in "
        + "a field, returned from a factory, or handed anywhere else is left alone, because it may already be the shared instance. "
        + "Set 'SocketsHttpHandler.PooledConnectionLifetime' on the shared instance if stale DNS is a concern; the entry point is "
        + "exempt, because a process that is about to exit exhausts nothing.";
}
