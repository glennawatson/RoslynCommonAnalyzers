// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1010 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1010 — key wrapping is implemented by hand rather than through the platform primitive.</summary>
    public static readonly DiagnosticDescriptor HandRolledKeyWrap = Create(
        "SES1010",
        "Do not implement AES key wrapping by hand",
        "This is the AES key-wrap integrity check value; wrap keys with 'Aes.EncryptKeyWrap' and 'Aes.DecryptKeyWrap' instead of by hand",
        Cryptography,
        HandRolledKeyWrapDescription);

    /// <summary>The SES1010 rule description.</summary>
    private const string HandRolledKeyWrapDescription =
        "The constant 0xA6A6A6A6A6A6A6A6 has one purpose: it is the default initial value AES key wrapping prepends to the "
        + "key before wrapping, and compares against after unwrapping to decide whether the result is genuine. Code holding "
        + "that constant is therefore implementing key wrap itself, and a hand-written implementation is exactly where this "
        + "goes wrong: the unwrap check has to be constant-time and has to fail closed, the block loop has to run the full "
        + "six passes in the right order, and getting any of it subtly wrong yields a routine that still round-trips its own "
        + "output while accepting forged or truncated wrapped keys. The platform ships a vetted implementation -- "
        + "'Aes.EncryptKeyWrap', 'Aes.DecryptKeyWrap', 'Aes.TryDecryptKeyWrap', and 'Aes.GetKeyWrapLength' -- which handles "
        + "the integrity value, the padding, and the comparison. Reported only when that API resolves in the compilation, so "
        + "a project that cannot yet call it is not told to.";
}
