// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1201 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1201 — a string literal holds what looks like a hard-coded credential, key, or token.</summary>
    public static readonly DiagnosticDescriptor HardcodedSecret = Create(
        "SES1201",
        "Do not hard-code secrets in source",
        "This string literal looks like a hard-coded {0}; move the secret into configuration or a secret store and rotate it",
        Secrets,
        HardcodedSecretDescription);

    /// <summary>The SES1201 rule description.</summary>
    private const string HardcodedSecretDescription =
        "A credential embedded in source is committed to version control, copied into every build artifact, and read by "
        + "anyone with the repository, so it must be treated as compromised the moment it lands and can only be secured by "
        + "rotating it. The rule reports a string literal whose content matches a high-precision credential shape -- an "
        + "OpenAI-style 'sk-' key, an 'AKIA' AWS access key id, a 'ghp_'/'gho_'/'ghu_'/'ghs_'/'ghr_' GitHub token, an 'xox' "
        + "Slack token, an 'AIza' Google API key, a PEM private-key block, an Azure shared-access or account-key body, or a "
        + "password value inside a connection string. It stays silent on ordinary text, on obvious placeholders "
        + "(angle-bracket templates, repeated fill characters, low-entropy or spelled-out stand-ins), and on strings shorter "
        + "than each shape's minimum length, so a match is almost always a real secret. Read the value from configuration, "
        + "user secrets, environment variables, or a managed secret store instead of hard-coding it.";
}
