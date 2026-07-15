// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2412 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2412 — a for loop steps its counter away from the side of its bound.</summary>
    public static readonly DiagnosticDescriptor LoopStepsAwayFromBound = Create(
        "SST2412",
        "A for loop should step its counter toward the bound",
        "The 'for' loop steps '{0}' away from the side its condition tests, so it never terminates or never runs",
        LoopStepsAwayFromBoundDescription);

    /// <summary>The LoopStepsAwayFromBound rule description.</summary>
    private const string LoopStepsAwayFromBoundDescription =
        "The counter's step and the side of the comparison disagree: an ascending step ('i++', 'i += 1') is paired with a 'greater "
        + "than' or 'greater than or equal' test, or a descending step ('i--', 'i -= 1') with a 'less than' or 'less than or equal' "
        + "test. Each iteration moves the counter further from the value that would end the loop, so it spins forever, or the condition "
        + "is already false and the body never runs. This is the reverse-loop typo: an index meant to walk backwards from the end "
        + "written with the forward comparison. Flipping the comparison operator restores the intended direction.";
}
