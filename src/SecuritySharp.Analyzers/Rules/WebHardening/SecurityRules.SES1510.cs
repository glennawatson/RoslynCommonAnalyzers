// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1510 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1510 — an MVC controller redirects to a non-constant URL (open redirect).</summary>
    public static readonly DiagnosticDescriptor NonConstantControllerRedirect = Create(
        "SES1510",
        "An MVC controller must not redirect to a non-constant URL",
        NonConstantControllerRedirectMessage,
        WebHardening,
        NonConstantControllerRedirectDescription);

    /// <summary>The SES1510 rule message.</summary>
    private const string NonConstantControllerRedirectMessage =
        "'{0}' redirects to a non-constant URL that an attacker can control; a redirect to an attacker-chosen host is an open "
        + "redirect that lands the user on a phishing site, so validate the target is a local URL (for example 'LocalRedirect')";

    /// <summary>The SES1510 rule description.</summary>
    private const string NonConstantControllerRedirectDescription =
        "'ControllerBase.Redirect', 'RedirectPermanent', 'RedirectPreserveMethod', and "
        + "'RedirectPermanentPreserveMethod' each write their argument straight into the response's 'Location' header. "
        + "When that argument is not a compile-time constant it can carry a value an attacker supplied -- a query-string "
        + "'returnUrl', a form field, a header -- and the browser will follow it to any host, including an external one. "
        + "That is an open redirect (CWE-601): a link that looks like it points at the trusted site lands the victim on an "
        + "attacker-controlled page that harvests credentials or serves malware, and the trusted domain in the original URL "
        + "lends it credibility. The rule fires only when the redirect target is non-constant; a hard-coded literal URL cannot "
        + "be steered by an attacker and is not reported. Prefer 'LocalRedirect' (and its 'Permanent'/'PreserveMethod' "
        + "variants), which reject a non-local URL, or validate the target against an allow-list of permitted destinations "
        + "before redirecting.";
}
