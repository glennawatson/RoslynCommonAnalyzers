// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1511 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1511 — the forwarded-headers trust boundary is removed, letting untrusted proxies spoof client values.</summary>
    public static readonly DiagnosticDescriptor ForwardedHeadersTrustBoundaryRemoval = Create(
        "SES1511",
        "The forwarded-headers trust boundary must not be removed",
        ForwardedHeadersTrustBoundaryRemovalMessage,
        WebHardening,
        ForwardedHeadersTrustBoundaryRemovalDescription);

    /// <summary>The SES1511 message format.</summary>
    private const string ForwardedHeadersTrustBoundaryRemovalMessage =
        "'{0}' removes the forwarded-headers trust restriction; the middleware then accepts 'X-Forwarded-*' headers from any "
        + "proxy, letting an untrusted client spoof its remote IP address, host, and scheme";

    /// <summary>The SES1511 rule description.</summary>
    private const string ForwardedHeadersTrustBoundaryRemovalDescription =
        "The forwarded-headers middleware rewrites a request's remote IP address, host, and scheme from the 'X-Forwarded-For', "
        + "'X-Forwarded-Host', and 'X-Forwarded-Proto' headers. Because any client can send those headers, the middleware trusts "
        + "them only from a known set of proxies -- loopback by default -- so a request that did not pass through a trusted reverse "
        + "proxy keeps its real connection values. Clearing 'KnownProxies' or the known-networks list, or setting 'ForwardLimit' to "
        + "null, removes that restriction: the middleware then applies forwarded headers from any source, so an attacker can forge "
        + "'X-Forwarded-For' to spoof the client IP that rate-limiting, allow-lists, and audit logs depend on, or forge the host and "
        + "scheme to defeat redirect and same-origin checks. Populate the trusted-proxy and trusted-network lists with only the "
        + "reverse proxies you operate, and keep a finite 'ForwardLimit' matching how many proxies a request legitimately traverses.";
}
