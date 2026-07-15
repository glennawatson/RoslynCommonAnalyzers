// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2413 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2413 — a for loop's condition is already false at its starting value.</summary>
    public static readonly DiagnosticDescriptor LoopBodyNeverRuns = Create(
        "SST2413",
        "A for loop's body should be able to run",
        "The 'for' loop's condition is false at the starting value of '{0}', so its body never runs",
        LoopBodyNeverRunsDescription);

    /// <summary>The LoopBodyNeverRuns rule description.</summary>
    private const string LoopBodyNeverRunsDescription =
        "The counter starts at a compile-time constant and is compared against another compile-time constant, and the comparison is "
        + "already false before the first iteration. The step direction is consistent with the comparison, so the loop is not stepping "
        + "the wrong way — the two constants are simply on the wrong sides of each other, usually a swapped start and bound. The body is "
        + "dead code. A condition that reads a collection's count is never reported: a collection that is empty today is not a mistake.";
}
