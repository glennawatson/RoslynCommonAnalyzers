// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1009 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1009 — a password is hashed with a fast general-purpose hash rather than a slow password KDF.</summary>
    public static readonly DiagnosticDescriptor FastPasswordHash = Create(
        "SES1009",
        "Passwords must use a slow, salted key-derivation function",
        FastPasswordHashMessage,
        Cryptography,
        FastPasswordHashDescription);

    /// <summary>The SES1009 message format.</summary>
    private const string FastPasswordHashMessage =
        "'{0}' is a password hashed with a fast general-purpose hash (MD5, SHA-1, SHA-256, SHA-384, or SHA-512); "
        + "a fast hash is cheap to brute-force, so derive it with a slow, salted password KDF such as "
        + "Rfc2898DeriveBytes/Pbkdf2 or Argon2 instead";

    /// <summary>The SES1009 rule description.</summary>
    private const string FastPasswordHashDescription =
        "A general-purpose hash such as MD5, SHA-1, SHA-256, SHA-384, or SHA-512 is built to be fast, which is exactly "
        + "what an attacker wants: modern hardware computes billions of these hashes per second, so a leaked digest of a "
        + "password falls to an offline dictionary or brute-force attack, and adding a salt does not slow the guessing "
        + "down. Passwords must instead be run through a deliberately slow, salted password key-derivation function "
        + "(Rfc2898DeriveBytes/Pbkdf2, Argon2, scrypt, or bcrypt) whose work factor keeps every guess expensive. The rule "
        + "fires only when the hashed input clearly reads as a password: the value passed to a fast hash's one-shot "
        + "'HashData' or instance 'ComputeHash' -- including a 'System.Text.Encoding.GetBytes(...)' wrapper around it -- "
        + "carries a name containing 'password', 'passwd', 'pwd', 'passphrase', or 'credential'. Hashing arbitrary, "
        + "non-password data is never flagged. There is no code fix because the correct replacement is a redesign: swap "
        + "the fast hash for a password KDF and persist its salt and work-factor parameters alongside the derived key.";
}
