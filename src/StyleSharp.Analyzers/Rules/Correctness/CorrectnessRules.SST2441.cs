// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2441 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2441 — a balanced placeholder carries no usable property name.</summary>
    public static readonly DiagnosticDescriptor MalformedPlaceholder = Create(
        "SST2441",
        "A message template placeholder should name a property",
        "The placeholder '{0}' has no valid property name, so its value is dropped from the log",
        MalformedPlaceholderDescription);

    /// <summary>The MalformedPlaceholder rule description.</summary>
    private const string MalformedPlaceholderDescription =
        "A message template placeholder must contain a property name so its value can be captured under that name. A placeholder that is "
        + "empty, whitespace, or holds text that is not a name — a space inside it, punctuation — captures nothing: the value is dropped "
        + "from the structured payload and the braces render as literal text in the message. The name is read after any leading capturing "
        + "operator is removed and any alignment or format suffix is set aside, so a formatted or destructured placeholder with a real name "
        + "is accepted. An unbalanced or stray brace is a separate defect the compiler already reports and is not covered here.";
}
