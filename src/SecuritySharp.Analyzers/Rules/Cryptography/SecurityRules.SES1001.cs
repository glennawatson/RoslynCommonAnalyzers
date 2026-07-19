// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1001 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1001 — an AEAD encryption call is given a constant or reused nonce.</summary>
    public static readonly DiagnosticDescriptor ConstantAeadNonce = Create(
        "SES1001",
        "AEAD encryption must not use a constant or reused nonce",
        "The nonce passed to '{0}.Encrypt' is a fixed value; a nonce reused with the same key breaks AEAD confidentiality and integrity",
        Cryptography,
        ConstantAeadNonceDescription);

    /// <summary>The SES1001 rule description.</summary>
    private const string ConstantAeadNonceDescription =
        "AES-GCM, AES-CCM, and ChaCha20-Poly1305 stay secure only while every message encrypted under one key uses a distinct "
        + "nonce. A fixed nonce -- an all-zero 'new byte[N]', a literal byte array, or a 'static readonly' field reused across "
        + "calls -- makes two messages under the same key share a keystream: an attacker who observes both recovers their XOR, "
        + "and for the Galois/Poly1305 tag can then forge authenticated ciphertext, so confidentiality and integrity both "
        + "collapse. Fill a fresh buffer from a cryptographic random source for every message and never hard-code the nonce.";
}
