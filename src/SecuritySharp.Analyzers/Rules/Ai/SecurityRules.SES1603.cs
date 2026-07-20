// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1603 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1603 — a model-facing tool declared read-only or non-destructive calls a state-changing API.</summary>
    public static readonly DiagnosticDescriptor NonDestructiveToolMutation = Create(
        "SES1603",
        "An AI tool declared read-only or non-destructive must not call a state-changing API",
        NonDestructiveToolMutationMessage,
        Ai,
        NonDestructiveToolMutationDescription);

    /// <summary>The SES1603 rule message.</summary>
    private const string NonDestructiveToolMutationMessage =
        "This tool is annotated read-only or non-destructive but its body calls '{0}', which deletes data, overwrites a file, "
        + "starts a process, or writes to a database; a model host may auto-invoke it and cause irreversible damage";

    /// <summary>The SES1603 rule description.</summary>
    private const string NonDestructiveToolMutationDescription =
        "A model-facing tool exposes read-only and non-destructive hints so the host can decide, without asking the user, "
        + "whether it is safe to invoke the tool on its own. A host is free to auto-run a tool it believes only reads state. "
        + "When such a tool actually deletes or overwrites a file, starts a process, or issues a destructive database command, "
        + "the model can trigger irreversible damage unattended. The rule fires only when the tool attribute explicitly declares "
        + "the tool read-only or non-destructive -- an unset hint defaults to destructive and is left alone -- and the method body "
        + "contains a call to one of a curated set of state-changing APIs (file delete/overwrite, directory delete, process start, "
        + "a destructive database command, or a change save). The match is local to the one method and never traces values across "
        + "calls. Either make the tool's behaviour match the advertised hint, or drop the read-only/non-destructive claim so the host "
        + "prompts before running it.";
}
