// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1515 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1515 — a Content-Security-Policy value permits inline, eval, or wildcard sources.</summary>
    public static readonly DiagnosticDescriptor PermissiveContentSecurityPolicy = Create(
        "SES1515",
        "A Content-Security-Policy value disables its own protection",
        PermissiveContentSecurityPolicyMessage,
        WebHardening,
        PermissiveContentSecurityPolicyDescription);

    /// <summary>The SES1515 rule message.</summary>
    private const string PermissiveContentSecurityPolicyMessage =
        "This Content-Security-Policy value allows {0}, so an injected inline script or an arbitrary source can execute and the "
        + "header no longer blocks cross-site scripting";

    /// <summary>The SES1515 rule description.</summary>
    private const string PermissiveContentSecurityPolicyDescription =
        "A Content-Security-Policy restricts where scripts, styles, and other resources may load from, and its central job is "
        + "blocking injected inline scripts -- the payload of most reflected and stored cross-site-scripting attacks. Adding "
        + "'unsafe-inline' to 'script-src' (or 'default-src'/'style-src') re-permits exactly those inline scripts; 'unsafe-eval' "
        + "re-permits string-to-code evaluation; and a bare '*' source lets script load from any origin. Any of the three turns "
        + "the policy into security theatre: the header is present but no longer stops the attack it exists to stop. This rule "
        + "inspects a string literal that is a Content-Security-Policy value -- it contains a policy directive ('default-src', "
        + "'script-src', 'style-src', 'object-src', or 'base-uri') and is either the value set on a 'Content-Security-Policy' "
        + "response header or begins with a directive itself -- and reports it when that value also contains 'unsafe-inline', "
        + "'unsafe-eval', or a bare '*' source. Remove the unsafe source and, for the inline scripts you genuinely need, prefer a "
        + "per-response nonce or a content hash instead.";
}
