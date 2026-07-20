// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1104 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1104 — X509 certificate-chain validation is deliberately weakened.</summary>
    public static readonly DiagnosticDescriptor WeakenedCertificateChainValidation = Create(
        "SES1104",
        "Certificate-chain validation must not be deliberately weakened",
        "X509 certificate-chain validation is weakened: 'X509ChainPolicy.{0}' is set to a value that suppresses genuine chain errors, so revoked or untrusted certificates are accepted",
        Transport,
        WeakenedCertificateChainValidationDescription);

    /// <summary>The SES1104 rule description.</summary>
    private const string WeakenedCertificateChainValidationDescription =
        "An 'X509Chain' validates a certificate by building the chain to a trusted root and checking each link, including "
        + "revocation status. Setting 'X509ChainPolicy.RevocationMode' to 'NoCheck' turns off revocation checking, so a "
        + "certificate revoked because its key was compromised is still accepted. Setting 'X509ChainPolicy.VerificationFlags' "
        + "to a value that names 'AllowUnknownCertificateAuthority' or 'AllFlags' -- alone or OR-combined with other flags -- "
        + "tells the chain engine to ignore untrusted-authority errors, so a certificate that does not chain to a trusted root, "
        + "including one an attacker forged, passes validation. Either weakening defeats the purpose of chain validation and "
        + "opens the connection to man-in-the-middle interception. Leave 'RevocationMode' at 'Online' or 'Offline' and "
        + "'VerificationFlags' at 'NoFlag'; if a specific check genuinely cannot apply, suppress only that narrow flag rather "
        + "than disabling authority trust or whole-chain revocation.";
}
