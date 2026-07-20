// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2490 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2490 — two adjacent <c>try</c> statements repeat the same catch/finally handling.</summary>
    public static readonly DiagnosticDescriptor MergeableAdjacentTry = Create(
        "SST2490",
        "Adjacent try statements with identical handling should be merged",
        "This 'try' repeats the catch/finally handling of the 'try' immediately before it; wrap both bodies in one 'try' so the shared handling is written once",
        MergeableAdjacentTryDescription);

    /// <summary>The MergeableAdjacentTry rule description.</summary>
    private const string MergeableAdjacentTryDescription =
        "Two 'try' statements sit next to each other in the same block, and the second carries exactly the same handling as the "
        + "first: the same catch clauses — count, caught type, exception filter, and handler body — and the same finally clause. The "
        + "duplicated handling is a maintenance trap: a later edit to one copy that misses the other leaves the two guarded regions "
        + "protected differently even though they read as a matched pair, and the repetition hides that both bodies were meant to run "
        + "under one guard. Wrapping both bodies in a single 'try' with one copy of the handling removes the duplication and states the "
        + "intent once. Only a genuinely non-trivial handler is reported: two adjacent 'try {} finally {}' with an empty finally, or a "
        + "bare 'try {} catch {}' with no caught type, filter, or handler body, guard nothing, so merging them buys nothing and is left "
        + "alone. No fix is offered because merging the bodies is not a mechanically safe edit — a local declared in the first body and "
        + "read in the second, or handling that must run between the two regions, can make the combined 'try' behave differently — so "
        + "the decision to merge is left to the author.";
}
