// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1005 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1005 — a secret is compared with a short-circuiting, non-constant-time equality.</summary>
    public static readonly DiagnosticDescriptor NonConstantTimeSecretComparison = Create(
        "SES1005",
        "Compare secret values in constant time",
        "'{0}' is compared with a short-circuiting equality; use CryptographicOperations.FixedTimeEquals so the comparison cannot be recovered a byte at a time through timing",
        Cryptography,
        NonConstantTimeSecretComparisonDescription);

    /// <summary>The SES1005 rule description.</summary>
    private const string NonConstantTimeSecretComparisonDescription =
        "An ordinary equality -- '==', '!=', '.Equals', 'object.Equals', or a 'SequenceEqual' -- returns as soon as the first "
        + "differing byte is found. When one side is a secret (an HMAC, signature, authentication tag, token, or hash) an "
        + "attacker who can submit guesses and measure how long the check takes recovers the expected value one byte at a "
        + "time, which is enough to forge a valid tag without ever knowing the key. The rule fires on a high-precision "
        + "name-and-type shape: one operand's identifier or member name contains 'hmac', 'signature', 'sig', 'mac', 'tag', "
        + "'token', 'hash', 'digest', or 'secret' (or 'expected'/'actual' inside a verify/validate/check-shaped method) and "
        + "both operands are 'byte[]', a byte span, or 'string'. Compare secrets with "
        + "'System.Security.Cryptography.CryptographicOperations.FixedTimeEquals', whose running time depends only on the "
        + "buffer length. The rule stays silent when that type is absent, so a target framework that cannot offer the fix "
        + "never receives a diagnostic it cannot act on.";
}
