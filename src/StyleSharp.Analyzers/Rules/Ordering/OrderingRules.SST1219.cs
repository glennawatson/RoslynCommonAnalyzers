// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST1219 descriptor.</summary>
internal static partial class OrderingRules
{
    /// <summary>SST1219 — a switch statement's default section is not last.</summary>
    public static readonly DiagnosticDescriptor DefaultSectionLast = Create(
        "SST1219",
        "A default section should appear last",
        "Move the 'default' section to the end of the 'switch'",
        DefaultSectionLastDescription);

    /// <summary>The DefaultSectionLast rule description.</summary>
    private const string DefaultSectionLastDescription =
        "A 'switch' statement's 'default' label sits ahead of one or more case sections. A reader expects the fall-through case at the "
        + "bottom, and putting it there is always safe: the language forbids implicit fall-through between sections, and 'goto case' / "
        + "'goto default' target a label rather than a position, so moving the 'default' section cannot change which code runs. A "
        + "'switch' expression enforces the same ordering at compile time, where a discard arm that is not last is an error.";
}
