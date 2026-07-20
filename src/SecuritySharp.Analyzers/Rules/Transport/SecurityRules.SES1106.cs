// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1106 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1106 — an HttpClient request targets a cleartext <c>http://</c> URL literal.</summary>
    public static readonly DiagnosticDescriptor CleartextHttpUrl = Create(
        "SES1106",
        "Do not send HttpClient requests to a cleartext http URL",
        "The request URL targets cleartext HTTP host '{0}'; data sent over http travels unencrypted and can be read or altered in transit -- use https",
        Transport,
        CleartextHttpUrlDescription);

    /// <summary>The SES1106 rule description.</summary>
    private const string CleartextHttpUrlDescription =
        "A request whose URL begins with 'http://' crosses the network unencrypted: anyone on the path can read the "
        + "request and response and can tamper with either, so credentials, tokens, and payloads are exposed and "
        + "responses can be silently replaced. The rule fires only on a hard-coded literal used at the call site -- the "
        + "URL string passed to an HttpClient request method, or the string inside a 'new Uri(...)' that is the request "
        + "argument or is assigned to 'HttpClient.BaseAddress' -- and stays silent for loopback hosts (localhost, "
        + "127.0.0.1, ::1, and any *.localhost host) where cleartext is expected in local development. Use an 'https://' "
        + "URL; switch the scheme only after confirming the endpoint serves TLS, since blindly changing it can break a "
        + "caller, which is why no automatic code fix is offered.";
}
