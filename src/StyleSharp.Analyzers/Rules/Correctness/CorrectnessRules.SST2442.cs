// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2442 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2442 — a template names the same placeholder twice.</summary>
    public static readonly DiagnosticDescriptor DuplicatePlaceholder = Create(
        "SST2442",
        "A message template should not repeat a placeholder name",
        "The placeholder name '{0}' appears more than once; later values overwrite earlier ones in the payload",
        DuplicatePlaceholderDescription);

    /// <summary>The DuplicatePlaceholder rule description.</summary>
    private const string DuplicatePlaceholderDescription =
        "A structured sink keys the logged payload by placeholder name, so two placeholders that share a name collapse to a single "
        + "property: the last value wins and the earlier one is lost. The message still reads plausibly, which is why the loss goes "
        + "unnoticed, and any query on that name returns only one of the values that were meant to be recorded. Names are compared without "
        + "regard to case, because a case-only difference is the same key to most sinks. The second and each later use of a repeated name "
        + "is reported; a purely numeric placeholder is positional and is not treated as a name.";
}
