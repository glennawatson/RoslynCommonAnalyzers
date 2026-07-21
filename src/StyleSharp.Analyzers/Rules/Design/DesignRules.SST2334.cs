// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2334 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2334 — a publicly visible type carries no debugger-display attribute; opt-in, disabled by default.</summary>
    public static readonly DiagnosticDescriptor MissingDebuggerDisplay = CreateDisabled(
        "SST2334",
        "Give a public type a debugger-display attribute",
        "'{0}' has no [DebuggerDisplay]; add one so the debugger shows a meaningful summary",
        MissingDebuggerDisplayDescription);

    /// <summary>The MissingDebuggerDisplay rule description.</summary>
    private const string MissingDebuggerDisplayDescription =
        "In the debugger, a type with no display attribute shows as its type name and nothing else, so a watch window full of them "
        + "tells you nothing without expanding each one. A '[DebuggerDisplay]' string pins the one or two fields that identify an "
        + "instance at a glance, which pays for itself the first time you scan a collection of them. This is an opinionated, heavy "
        + "nudge — most types never need it and it would fire on nearly every public type — so it is off by default and opt-in "
        + "through '.editorconfig'.";
}
