// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1501 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1501 — a CORS policy allows any origin and also allows credentials.</summary>
    public static readonly DiagnosticDescriptor CorsAnyOriginWithCredentials = Create(
        "SES1501",
        "A CORS policy must not allow credentials together with any origin",
        "This CORS policy calls both 'AllowAnyOrigin' and 'AllowCredentials'; a wildcard origin with credentials is rejected by browsers and throws when the policy is applied",
        WebHardening,
        CorsAnyOriginWithCredentialsDescription);

    /// <summary>The SES1501 rule description.</summary>
    private const string CorsAnyOriginWithCredentialsDescription =
        "A CORS policy that calls 'AllowAnyOrigin' emits 'Access-Control-Allow-Origin: *', and one that calls 'AllowCredentials' "
        + "emits 'Access-Control-Allow-Credentials: true'. The Fetch standard forbids that pair: a browser rejects a credentialed "
        + "response whose allowed origin is the wildcard, and ASP.NET Core itself throws an 'InvalidOperationException' when such a "
        + "policy is applied to a request, so the endpoint fails at run time. The combination is also a classic misconfiguration -- "
        + "an attempt to 'open up' CORS that instead breaks every credentialed cross-origin call. Reflect and allow only the specific "
        + "origins that must send credentials (for example 'WithOrigins(...)') instead of allowing any origin.";
}
