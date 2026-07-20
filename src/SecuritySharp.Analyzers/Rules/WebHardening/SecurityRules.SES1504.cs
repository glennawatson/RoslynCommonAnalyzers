// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1504 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1504 — a cookie initializer sets <c>SameSite = None</c> without marking the cookie secure.</summary>
    public static readonly DiagnosticDescriptor SameSiteNoneWithoutSecure = Create(
        "SES1504",
        "A cookie with SameSite=None must be marked Secure",
        SameSiteNoneWithoutSecureMessage,
        WebHardening,
        SameSiteNoneWithoutSecureDescription);

    /// <summary>The SES1504 rule message.</summary>
    private const string SameSiteNoneWithoutSecureMessage =
        "The '{0}' initializer sets 'SameSite' to 'None' but does not mark the cookie secure; a SameSite=None cookie is dropped "
        + "by modern browsers unless the Secure attribute is set, and without it the cookie also travels over plain HTTP";

    /// <summary>The SES1504 rule description.</summary>
    private const string SameSiteNoneWithoutSecureDescription =
        "'SameSite=None' asks the browser to send the cookie on cross-site requests. That relaxation is only safe when the "
        + "cookie is also marked Secure so it is confined to HTTPS: modern browsers reject a 'SameSite=None' cookie that lacks "
        + "the Secure attribute outright, and a cookie without Secure is transmitted over plain HTTP where a network attacker can "
        + "read or replay it. This rule reports a single object initializer of 'Microsoft.AspNetCore.Http.CookieOptions' or "
        + "'Microsoft.AspNetCore.Http.CookieBuilder' that sets 'SameSite' to 'SameSiteMode.None' without a sibling member that "
        + "secures the cookie in that same initializer -- 'Secure = true' on 'CookieOptions', or a non-'None' 'SecurePolicy' on "
        + "'CookieBuilder'. Set the securing member alongside 'SameSite', or choose 'SameSiteMode.Lax'/'Strict' if the cookie "
        + "does not need to be sent cross-site.";
}
