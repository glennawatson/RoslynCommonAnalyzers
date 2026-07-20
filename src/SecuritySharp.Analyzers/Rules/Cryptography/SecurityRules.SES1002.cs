// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1002 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1002 — a password-based key-derivation call is given a constant or predictable salt.</summary>
    public static readonly DiagnosticDescriptor ConstantKdfSalt = Create(
        "SES1002",
        "Password-based key derivation must not use a constant or predictable salt",
        "The salt passed to {0} is a fixed value; a predictable salt lets an attacker precompute a rainbow table and defeats the per-secret uniqueness a salt exists to provide",
        Cryptography,
        ConstantKdfSaltDescription);

    /// <summary>The SES1002 rule description.</summary>
    private const string ConstantKdfSaltDescription =
        "PBKDF2 (via 'Rfc2898DeriveBytes' or the static 'Rfc2898DeriveBytes.Pbkdf2') derives a key from a password and a salt. "
        + "The salt exists so that the same password produces a different derived key for every credential, which forces an "
        + "attacker to attack each hash on its own and makes precomputed rainbow tables useless. A fixed salt -- an all-zero "
        + "'new byte[N]', a literal or constant byte array, a 'static readonly' field shared across calls, or "
        + "'Encoding.GetBytes' over a string literal -- throws that away: one precomputed table now cracks every password hashed "
        + "with it, and identical passwords produce identical derived keys. Generate a fresh cryptographically random salt for "
        + "each credential (for example 'RandomNumberGenerator.GetBytes(16)'), store it alongside the hash, and never hard-code it.";
}
