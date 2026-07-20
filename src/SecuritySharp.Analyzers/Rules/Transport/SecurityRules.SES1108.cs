// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1108 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1108 — a custom server-certificate callback that always returns true disables TLS authentication.</summary>
    public static readonly DiagnosticDescriptor AlwaysTrueServerCertificateValidation = Create(
        "SES1108",
        "Do not accept every server certificate from a custom callback",
        "This 'ServerCertificateCustomValidationCallback' returns true for every certificate, disabling TLS server authentication and exposing the connection to man-in-the-middle attacks",
        Transport,
        AlwaysTrueServerCertificateValidationDescription);

    /// <summary>The SES1108 rule description.</summary>
    private const string AlwaysTrueServerCertificateValidationDescription =
        "TLS protects a connection only while the client verifies that the server's certificate chains to a trusted root and "
        + "matches the host. Assigning 'HttpClientHandler.ServerCertificateCustomValidationCallback' a callback that always "
        + "returns true -- an expression lambda '(message, cert, chain, errors) => true', a block lambda or anonymous method "
        + "whose only result is 'return true;', or a method group to a source method of that shape -- replaces that "
        + "verification with unconditional acceptance: the client completes a handshake with any server presenting a "
        + "self-signed, expired, or wrong-host certificate, and every request and response can then be read and altered in "
        + "transit. The body is inspected only locally, so a callback that actually checks the certificate is never reported. "
        + "Remove the callback and let the platform validate the chain; if a specific self-signed or pinned certificate must "
        + "be trusted, compare that one certificate (for example by thumbprint) and return false for everything else. The "
        + "built-in 'DangerousAcceptAnyServerCertificateValidator' is reported separately.";
}
