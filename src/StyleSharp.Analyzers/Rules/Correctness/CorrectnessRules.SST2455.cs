// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2455 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2455 — two enum members hold the same value without saying they are the same thing.</summary>
    public static readonly DiagnosticDescriptor DuplicateEnumValue = Create(
        "SST2455",
        "Enum members should not silently share a value",
        "'{0}' has the same value as '{1}'",
        DuplicateEnumValueDescription);

    /// <summary>The DuplicateEnumValue rule description.</summary>
    private const string DuplicateEnumValueDescription =
        "Two members of an enum that hold the same number are the same value: they compare equal, a switch "
        + "cannot have a section for each, and ToString gives whichever the runtime picks. When that is "
        + "intended it is written as an alias — 'Default = Read' — which says so and stays correct if the "
        + "underlying number changes. A bare literal that happens to repeat an earlier member's number is "
        + "almost always a copy-paste or a renumbering that missed one. Reported only for the second and later "
        + "members whose value is not expressed by naming another member of the same enum, so a deliberate "
        + "alias is never flagged.";
}
