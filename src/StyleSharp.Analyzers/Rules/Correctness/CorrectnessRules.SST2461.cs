// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2461 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2461 — a combined flags value sets a bit no member of the enum defines.</summary>
    public static readonly DiagnosticDescriptor UndefinedFlagInCompositeValue = Create(
        "SST2461",
        "A combined flags value should only set bits the enum defines",
        "'{0}' sets bit {1}, which no member of this enum defines",
        UndefinedFlagInCompositeValueDescription);

    /// <summary>The UndefinedFlagInCompositeValue rule description.</summary>
    private const string UndefinedFlagInCompositeValueDescription =
        "A composite member of a '[Flags]' enum is supposed to be the union of members that exist. When its "
        + "number sets a bit no member defines, that bit belongs to nothing: 'HasFlag' answers true for a value "
        + "no name describes, 'ToString' prints the number instead of a name, and a later member added on that "
        + "bit silently becomes part of the composite. It is nearly always a hand-written literal that drifted "
        + "from the members it was meant to combine — 'All = 7' left behind when the third flag was deleted, or "
        + "written before it was added. Reported for a member of a '[Flags]' enum whose value sets a bit that no "
        + "single-bit member of the same enum declares.";
}
