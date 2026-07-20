// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1505 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1505 — the request body size limit is removed, allowing an unbounded upload.</summary>
    public static readonly DiagnosticDescriptor RequestBodySizeLimitRemoval = Create(
        "SES1505",
        "The request body size limit must not be removed",
        RequestBodySizeLimitRemovalMessage,
        WebHardening,
        RequestBodySizeLimitRemovalDescription);

    /// <summary>The SES1505 message format.</summary>
    private const string RequestBodySizeLimitRemovalMessage =
        "'{0}' removes the request body size limit; a client can then stream an unbounded request body and exhaust server memory or disk (denial of service)";

    /// <summary>The SES1505 rule description.</summary>
    private const string RequestBodySizeLimitRemovalDescription =
        "ASP.NET Core caps the size of an incoming request body so a single client cannot stream an arbitrarily large payload "
        + "into server memory or a temporary file. Removing that cap -- applying '[DisableRequestSizeLimit]' to a controller or "
        + "action, or setting 'KestrelServerLimits.MaxRequestBodySize' or 'IHttpMaxRequestBodySizeFeature.MaxRequestBodySize' to "
        + "null, which both mean 'no limit' -- lets an attacker upload an unbounded body and exhaust the process's memory or disk, "
        + "taking the service down. Keep a finite limit sized to the largest legitimate upload, and raise it deliberately only for "
        + "the specific endpoints that need it rather than removing it outright.";
}
