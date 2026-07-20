// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1004 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1004 — a secret is produced from <c>Guid.NewGuid()</c> rather than a cryptographic RNG.</summary>
    public static readonly DiagnosticDescriptor GuidAsSecret = Create(
        "SES1004",
        "A secret must not be produced from Guid.NewGuid()",
        "'{0}' takes its value from Guid.NewGuid(); a GUID is an identifier, not a cryptographically strong secret -- produce it with RandomNumberGenerator instead",
        Cryptography,
        GuidAsSecretDescription);

    /// <summary>The SES1004 rule description.</summary>
    private const string GuidAsSecretDescription =
        "A GUID is designed to be unique, not unguessable. It carries at most 122 bits of entropy, its layout is public, "
        + "and it is routinely logged, embedded in URLs, and returned in responses -- none of which is a problem for an "
        + "identifier but all of which is fatal for a secret. Using 'Guid.NewGuid()' to mint a bearer token, API key, "
        + "password, nonce, salt, session id, one-time code, or password-reset token gives an attacker a value that is "
        + "far weaker than it looks and, on some platforms, is not drawn from a cryptographic source at all. The rule "
        + "fires only when the GUID's value flows directly into a local, field, property, parameter, or return whose name "
        + "matches a curated, high-precision secret vocabulary, so an ordinary GUID used as an id is never flagged. Draw "
        + "the value from 'System.Security.Cryptography.RandomNumberGenerator' -- 'GetBytes'/'GetInt32', or "
        + "'GetHexString'/'GetString' where they are available -- and size it for the secret you need.";
}
