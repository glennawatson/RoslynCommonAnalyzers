// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1003 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1003 — a PBKDF2 one-shot derives a key with a constant, too-low iteration count.</summary>
    public static readonly DiagnosticDescriptor Pbkdf2IterationCount = Create(
        "SES1003",
        "Password-based key derivation must use a sufficient iteration count",
        "This PBKDF2 call derives a key with only {0} iterations; use at least {1} so offline password cracking stays expensive",
        Cryptography,
        Pbkdf2IterationCountDescription);

    /// <summary>The SES1003 rule description.</summary>
    private const string Pbkdf2IterationCountDescription =
        "PBKDF2 resists a stolen-hash offline attack only by making each password guess deliberately slow, and the iteration "
        + "count is the whole cost knob: the derivation runs the pseudo-random function that many times per guess. A constant "
        + "count that is far below a modern floor makes billions of guesses per second cheap on commodity hardware, so a leaked "
        + "salt and derived key are brute-forced back to the password. This rule reports the 'iterations' argument of a "
        + "'System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2' one-shot call when it is a compile-time constant strictly "
        + "below the configured floor (default 100000). A non-constant count -- read from configuration or computed at runtime "
        + "-- is left alone, because its value cannot be judged from the source. Pass a fixed count at or above the floor "
        + "(raise it as hardware gets faster), and configure the floor per project with 'securitysharp.SES1003.iterations'.";
}
