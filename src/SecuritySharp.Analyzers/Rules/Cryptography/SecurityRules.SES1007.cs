// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1007 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1007 — a cryptographic primitive is implemented by hand by deriving from an abstract primitive base.</summary>
    public static readonly DiagnosticDescriptor HomeRolledCryptography = Create(
        "SES1007",
        "Do not hand-implement a cryptographic primitive",
        "'{0}' derives from the abstract cryptographic base '{1}' and supplies a primitive by hand; use a platform-provided algorithm instead of rolling your own",
        Cryptography,
        HomeRolledCryptographyDescription);

    /// <summary>The SES1007 rule description.</summary>
    private const string HomeRolledCryptographyDescription =
        "The abstract base classes in 'System.Security.Cryptography' -- 'HashAlgorithm', 'KeyedHashAlgorithm', 'HMAC', "
        + "'SymmetricAlgorithm', 'AsymmetricAlgorithm', and 'DeriveBytes' -- exist to be overridden with the actual "
        + "transform, so a type that derives from one of them is supplying a hash, keyed hash, cipher, key-exchange, or "
        + "key-derivation algorithm of its own. Writing a cryptographic primitive by hand is a well-known source of severe, "
        + "hard-to-spot defects: timing side channels, weak or biased constructions, incorrect padding and block handling, "
        + "and non-constant-time comparisons. These flaws pass every functional test yet leave the data unprotected, and they "
        + "are exactly what the vetted platform implementations already get right. Use a built-in algorithm -- 'SHA256', "
        + "'Aes', 'HMACSHA256', 'RSA', 'Rfc2898DeriveBytes', and the rest -- or a reviewed cryptographic library instead. "
        + "Subclassing a concrete, named algorithm to configure it -- rather than deriving from the abstract primitive -- is "
        + "not reported, because that reuses the vetted implementation instead of replacing it. The rule walks the base chain, "
        + "so a class reached through a custom intermediate base is still caught, while a class whose chain passes through a "
        + "concrete algorithm first is left alone.";
}
