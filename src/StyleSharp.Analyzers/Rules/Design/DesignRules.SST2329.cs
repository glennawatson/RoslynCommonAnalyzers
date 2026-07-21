// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2329 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2329 — a flags enum declares no zero-valued member.</summary>
    public static readonly DiagnosticDescriptor FlagsEnumMissingZeroValue = Create(
        "SST2329",
        "Flags enums should declare a zero value",
        "'{0}' is marked [Flags] but declares no zero value; add 'None = 0'",
        FlagsEnumMissingZeroValueDescription);

    /// <summary>The FlagsEnumMissingZeroValue rule description.</summary>
    private const string FlagsEnumMissingZeroValueDescription =
        "A flags enum is a set, and every set has an empty member. Without a zero value there is no name for 'no flags set', so "
        + "callers compare against a magic '(MyFlags)0', a default-initialized field prints as an empty string rather than a "
        + "meaningful name, and 'HasFlag' against the empty set has nothing to ask for. Declare a zero member — 'None = 0' by "
        + "convention — so the empty set has a name the same way every other combination does.";
}
