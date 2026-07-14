// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2406 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2406 — a for loop's stop condition can never change.</summary>
    public static readonly DiagnosticDescriptor InvariantLoopCondition = Create(
        "SST2406",
        "A loop's stop condition should be able to change",
        "Nothing in this loop can change '{0}', so it runs forever or not at all",
        InvariantLoopConditionDescription);

    /// <summary>The InvariantLoopCondition rule description.</summary>
    private const string InvariantLoopConditionDescription =
        "A loop whose condition reads only values the body never touches has already decided its answer before it starts. Either it never "
        + "runs, or it never stops — and the variable that was supposed to advance is the one that was forgotten.";
}
