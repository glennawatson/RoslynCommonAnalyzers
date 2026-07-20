// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1601 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1601 — an LLM system-role message is given non-constant content.</summary>
    public static readonly DiagnosticDescriptor NonConstantSystemPrompt = Create(
        "SES1601",
        "An LLM system prompt must be a constant, trusted template",
        "The system-role content passed to '{0}' is not a compile-time constant; runtime or user data placed in an LLM instruction channel enables prompt injection",
        Ai,
        NonConstantSystemPromptDescription);

    /// <summary>The SES1601 rule description.</summary>
    private const string NonConstantSystemPromptDescription =
        "A chat model treats its system-role message as the trusted instruction channel: the rules, persona, and guardrails "
        + "the model must obey. Content that is not a compile-time constant -- a method parameter, a field, an interpolated "
        + "string, or any value computed at runtime -- can carry attacker-controlled text into that channel, and text there "
        + "outranks the user turn, so an injected 'ignore your instructions' overrides the guardrails and the model leaks data "
        + "or performs actions it was told to refuse. Keep the system message a fixed, literal template and route every piece "
        + "of runtime or user data through the user-role message instead. The rule reports a 'Microsoft.Extensions.AI.ChatMessage' "
        + "built with 'ChatRole.System', and a Semantic Kernel 'ChatHistory.AddSystemMessage' / 'ChatHistory.AddMessage(AuthorRole.System, ...)' "
        + "/ 'ChatMessageContent(AuthorRole.System, ...)', whose content argument has no constant value. It resolves those "
        + "types once per compilation and stays silent when neither AI library is referenced.";
}
