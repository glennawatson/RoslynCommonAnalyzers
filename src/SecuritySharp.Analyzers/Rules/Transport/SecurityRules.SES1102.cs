// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1102 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1102 — the accept-any server-certificate validator disables TLS authentication.</summary>
    public static readonly DiagnosticDescriptor AcceptAnyServerCertificate = Create(
        "SES1102",
        "Do not accept any server certificate",
        "'HttpClientHandler.DangerousAcceptAnyServerCertificateValidator' accepts every server certificate, disabling TLS authentication and exposing the connection to man-in-the-middle attacks",
        Transport,
        AcceptAnyServerCertificateDescription);

    /// <summary>The SES1102 rule description.</summary>
    private const string AcceptAnyServerCertificateDescription =
        "TLS protects a connection only while the client verifies that the server's certificate chains to a trusted root and "
        + "matches the host. 'HttpClientHandler.DangerousAcceptAnyServerCertificateValidator' is a built-in callback that returns "
        + "true for every certificate, so wiring it into 'ServerCertificateCustomValidationCallback' (or any equivalent validation "
        + "callback) turns that verification off entirely: the client will complete a handshake with an attacker presenting a "
        + "self-signed or wrong-host certificate, and every request and response can then be read and altered in transit. Reading "
        + "the member has no other purpose, so any reference to it is reported. Remove it and let the platform validate the chain; "
        + "if a specific self-signed or pinned certificate must be trusted, validate that one certificate explicitly instead of "
        + "accepting all of them.";
}
