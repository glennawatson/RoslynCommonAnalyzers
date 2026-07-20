// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1604 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1604 — a prompt template disables the default encoding of substituted variables.</summary>
    public static readonly DiagnosticDescriptor PromptTemplateContentEncodingDisabled = Create(
        "SES1604",
        "Prompt-template input encoding must not be disabled",
        "'{0}.AllowDangerouslySetContent' is set to true, so substituted variables are no longer encoded and injected content can break out of its template slot",
        Ai,
        PromptTemplateContentEncodingDisabledDescription);

    /// <summary>The SES1604 rule description.</summary>
    private const string PromptTemplateContentEncodingDisabledDescription =
        "A Semantic Kernel prompt template treats every substituted input variable and function return value as untrusted and "
        + "HTML/expression-encodes it before it reaches the model, so injected text cannot close the current message tag or open a "
        + "new one and hijack the conversation. Setting 'AllowDangerouslySetContent = true' on the prompt-template configuration, on "
        + "an individual input variable, or on the template factory turns that encoding off and inserts the raw value verbatim: an "
        + "attacker who controls the variable can then inject '</message><message role=\"system\">' and rewrite the system prompt, "
        + "exfiltrate context, or invoke tools the user never authorised. Leave the flag at its secure default so the value stays "
        + "encoded, and gate any use of a chat-completion service behind that encoding; only trust content that cannot carry "
        + "attacker input (for example a fixed template you author, not a user-supplied variable).";
}
