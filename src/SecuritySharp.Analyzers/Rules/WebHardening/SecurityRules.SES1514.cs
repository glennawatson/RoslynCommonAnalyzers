// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1514 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1514 — an OpenID Connect PKCE, state, or nonce protection is disabled.</summary>
    public static readonly DiagnosticDescriptor OidcProtocolProtectionDisabled = Create(
        "SES1514",
        "OpenID Connect PKCE, state, and nonce protections must not be disabled",
        OidcProtocolProtectionDisabledMessage,
        WebHardening,
        OidcProtocolProtectionDisabledDescription);

    /// <summary>The SES1514 message format.</summary>
    private const string OidcProtocolProtectionDisabledMessage =
        "'{0}.{1}' is set to false, which turns off an OpenID Connect authorization-code-flow protection and lets the sign-in be "
        + "forged (CSRF) or a captured response replayed";

    /// <summary>The SES1514 rule description.</summary>
    private const string OidcProtocolProtectionDisabledDescription =
        "The OpenID Connect authorization-code flow defends the sign-in with three protocol protections that all default to on. "
        + "PKCE ('OpenIdConnectOptions.UsePkce') binds the code exchange to the client that started the flow, so a stolen "
        + "authorization code cannot be redeemed by anyone else. The state parameter ('OpenIdConnectProtocolValidator.RequireState' "
        + "and 'RequireStateValidation') ties the provider's redirect back to the request the browser actually began, which is what "
        + "stops a cross-site request forgery login. The nonce ('OpenIdConnectProtocolValidator.RequireNonce') binds the returned "
        + "id token to this one authentication so a previously captured token cannot be replayed. Assigning false to any of these "
        + "flags -- directly or in an object initializer -- silently downgrades the flow and reopens the CSRF and replay holes the "
        + "protocol was designed to close. Leave each flag at its secure default. The 'OpenIdConnectProtocolValidator' is reached "
        + "through 'OpenIdConnectOptions.ProtocolValidator', and the rule resolves both types in the compilation, so a project that "
        + "does not use OpenID Connect authentication receives no diagnostic it cannot act on.";
}
