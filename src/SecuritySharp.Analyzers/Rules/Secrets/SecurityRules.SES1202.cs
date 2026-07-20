// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1202 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1202 — a non-empty string literal is bound to a credential parameter or credential-type constructor.</summary>
    public static readonly DiagnosticDescriptor HardcodedCredentialArgument = Create(
        "SES1202",
        "Do not hard-code a credential value",
        "A string literal is passed where a credential is expected ('{0}'); move the secret to configuration, an environment variable, or a secret store",
        Secrets,
        HardcodedCredentialArgumentDescription);

    /// <summary>The SES1202 rule description.</summary>
    private const string HardcodedCredentialArgumentDescription =
        "A secret written directly into source is compiled into the assembly, committed to version control, and shared with "
        + "everyone who can read the repository or decompile the binary; it cannot be rotated without a rebuild, and a leak of "
        + "the source is a leak of the credential. This rule reports a non-empty string literal in a position that expects a "
        + "credential -- an argument bound to a parameter named like one (apiKey, password, secret, token, connectionString, "
        + "accessKey, privateKey, and similar), or the secret position of a known credential-type constructor "
        + "(NetworkCredential, AzureKeyCredential, ClientSecretCredential, ApiKeyCredential). It complements the pattern-based "
        + "secret rule by catching a hard-coded credential even when its text is not a recognizable secret shape. Obvious "
        + "placeholders (an empty string, a 'your-...' or '<...>' template, 'changeme', or an all-same-character mask) and any "
        + "non-literal (a variable, a configuration lookup, an environment read) are not reported, because those are the "
        + "correct way to supply a secret. Read the value from configuration, an environment variable, or a secret manager "
        + "instead of embedding it in the code.";
}
