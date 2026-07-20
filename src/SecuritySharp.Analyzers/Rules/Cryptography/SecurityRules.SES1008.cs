// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1008 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1008 — an XML signature is verified against the key embedded in the document rather than a known one.</summary>
    public static readonly DiagnosticDescriptor UntrustedXmlSignatureKey = Create(
        "SES1008",
        "Verify an XML signature against a known key",
        "'{0}' verifies the XML signature with the key embedded in the document, which an attacker can swap for their own; pass a known key or certificate to CheckSignature",
        Cryptography,
        UntrustedXmlSignatureKeyDescription);

    /// <summary>The SES1008 rule description.</summary>
    private const string UntrustedXmlSignatureKeyDescription =
        "'SignedXml.CheckSignature()' and 'CheckSignature(bool)' take no caller-supplied key, so they verify the signature "
        + "against the public key or certificate carried inside the document's own 'KeyInfo' element. That proves only that "
        + "whoever holds the embedded key signed the document -- not that a trusted party did. An attacker can strip the real "
        + "signature, tamper with the XML, re-sign it with a key pair they generated, embed their own public key in 'KeyInfo', "
        + "and the check still returns 'true'. Verify against a key you already trust: pass the expected public key to "
        + "'CheckSignature(AsymmetricAlgorithm)', or pass a certificate you have pinned or chain-validated to "
        + "'CheckSignature(X509Certificate2, bool)'. If you must read the signing key or certificate from the document, use an "
        + "overload that returns it and validate it against a trust anchor before trusting the result. The rule is gated on "
        + "'System.Security.Cryptography.Xml.SignedXml' resolving, so a project without the XML-signing package pays nothing.";
}
