// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The Blazor group (SES17xx) descriptors: SES1707, SES1708, SES1709, and SES1710.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1707 — a secret-shaped string literal sits in an assembly that downloads to the browser as WebAssembly.</summary>
    public static readonly DiagnosticDescriptor WebAssemblyHardcodedSecret = Create(
        "SES1707",
        "Do not hard-code secrets in code that runs in the browser as WebAssembly",
        WebAssemblyHardcodedSecretMessage,
        Blazor,
        WebAssemblyHardcodedSecretDescription);

    /// <summary>SES1708 — server circuit exception detail is shipped to every connected client.</summary>
    public static readonly DiagnosticDescriptor CircuitDetailedErrorsEnabled = Create(
        "SES1708",
        "Circuit detailed errors must not be enabled outside development",
        "'CircuitOptions.DetailedErrors' is set to true, which sends full server exception detail and stack traces to every connected browser; enable it only in the Development environment",
        Blazor,
        CircuitDetailedErrorsEnabledDescription);

    /// <summary>SES1709 — every claim is serialized into the client-readable WebAssembly authentication state.</summary>
    public static readonly DiagnosticDescriptor SerializeAllClaimsEnabled = Create(
        "SES1709",
        "Do not serialize every claim into the client-readable authentication state",
        SerializeAllClaimsEnabledMessage,
        Blazor,
        SerializeAllClaimsEnabledDescription);

    /// <summary>SES1710 — antiforgery validation is turned off for a form or component.</summary>
    public static readonly DiagnosticDescriptor AntiforgeryValidationDisabled = Create(
        "SES1710",
        "Do not disable antiforgery validation on a form",
        AntiforgeryValidationDisabledMessage,
        Blazor,
        AntiforgeryValidationDisabledDescription);

    /// <summary>The SES1707 rule message.</summary>
    private const string WebAssemblyHardcodedSecretMessage =
        "This string literal looks like a hard-coded {0} in code that runs in the browser as WebAssembly; the assembly is downloaded "
        + "to the client and fully readable, so treat the secret as disclosed -- keep it on the server and reach it over an authenticated call";

    /// <summary>The SES1709 rule message.</summary>
    private const string SerializeAllClaimsEnabledMessage =
        "'AuthenticationStateSerializationOptions.SerializeAllClaims' is set to true, which copies every claim -- including internal "
        + "identifiers, tokens, and personal data -- into the WebAssembly authentication state that is readable in the browser; serialize "
        + "only the claims the client needs";

    /// <summary>The SES1710 rule message.</summary>
    private const string AntiforgeryValidationDisabledMessage =
        "'[RequireAntiforgeryToken(required: false)]' turns off antiforgery (CSRF) validation for this form's posts, so a forged "
        + "cross-site request is accepted; remove the argument so the antiforgery token is required";

    /// <summary>The SES1707 rule description.</summary>
    private const string WebAssemblyHardcodedSecretDescription =
        "A Blazor WebAssembly assembly is downloaded to the browser and runs on the client, so every string it contains is "
        + "fully readable by anyone who opens the developer tools or unpacks the app bundle. A credential embedded in that "
        + "code is therefore disclosed the moment the app is served -- there is no server boundary left to protect it -- and "
        + "the identical literal that would be safe in server-only code becomes a guaranteed leak here. This rule reports a "
        + "string literal whose content matches a high-precision credential shape (the same shapes as the general hard-coded "
        + "secret rule: an OpenAI-style key, an AWS access key id, a GitHub or Slack token, a Google API key, a PEM "
        + "private-key block, an Azure key body, or a connection-string password) when it appears in a WebAssembly-reachable "
        + "compilation: a standalone WebAssembly host (or the client project of a Blazor Web App), whose whole assembly is "
        + "downloaded, or a component whose type carries an Interactive WebAssembly or Interactive Auto render-mode attribute. "
        + "Server-rendered code is not reported, because that text never leaves the server. Keep the secret on the server, "
        + "expose it only through an authenticated endpoint, and have the client call that endpoint.";

    /// <summary>The SES1708 rule description.</summary>
    private const string CircuitDetailedErrorsEnabledDescription =
        "A server-side Blazor circuit normally sends the browser only a generic error identifier when an unhandled exception "
        + "occurs, keeping the message and stack trace in the server log. Setting 'CircuitOptions.DetailedErrors' to true "
        + "reverses that: the full exception detail -- message, stack trace, and any inner state surfaced in it, such as file "
        + "paths, type and framework versions, and connection strings caught in a message -- is streamed to every connected "
        + "client. That is a debugging aid for a developer and a reconnaissance gift for an attacker, so it belongs only in the "
        + "Development environment. This rule reports 'DetailedErrors' assigned the constant 'true' on the "
        + "'Microsoft.AspNetCore.Components.Server.CircuitOptions' type, whether written directly or as an object-initializer "
        + "member. Leave the option at its default in production, or bind it to the environment so it is enabled only in "
        + "Development.";

    /// <summary>The SES1709 rule description.</summary>
    private const string SerializeAllClaimsEnabledDescription =
        "When a Blazor Web App serializes the server authentication state so a WebAssembly client can read it, the default "
        + "serializer emits only the name and role claims -- the minimum the browser needs to render an authorized view. "
        + "Setting 'AuthenticationStateSerializationOptions.SerializeAllClaims' to true instead copies every claim on the "
        + "principal into that state, and because the state is transported to and stored in the browser it becomes fully "
        + "readable on the client. Internal identifiers, access or refresh tokens, security stamps, and personal data that "
        + "were only ever meant for the server are then disclosed to anyone with the client. This rule reports "
        + "'SerializeAllClaims' assigned the constant 'true' on the "
        + "'Microsoft.AspNetCore.Components.WebAssembly.Server.AuthenticationStateSerializationOptions' type, whether written "
        + "directly or as an object-initializer member. Leave it at its default and, when the client genuinely needs an extra "
        + "claim, add just that claim through the serialization callback rather than shipping them all.";

    /// <summary>The SES1710 rule description.</summary>
    private const string AntiforgeryValidationDisabledDescription =
        "Antiforgery (anti-CSRF) validation checks that a state-changing form post carries a token the server issued to the "
        + "same user, which is what stops a malicious page from silently submitting a request on the victim's behalf. A form "
        + "or component is protected by default; passing 'required: false' to the '[RequireAntiforgeryToken]' attribute turns "
        + "that check off, so a forged cross-site post is accepted and processed. This rule reports the attribute -- applied "
        + "to a component or a method -- when its 'required' argument is the constant 'false', in either the named "
        + "('required: false') or positional form. Remove the argument (or pass 'true') so the antiforgery token is validated; "
        + "disable it only for an endpoint that changes no state and holds no session, and never for a public app.";
}
