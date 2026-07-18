// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2465 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2465 — a for loop's body reassigns a variable its condition depends on.</summary>
    public static readonly DiagnosticDescriptor LoopConditionVariableReassigned = Create(
        "SST2465",
        "A for loop's body reassigns a variable its condition depends on",
        "The loop body reassigns '{0}', which the for condition tests, so the loop runs a different number of times than its header states",
        LoopConditionVariableReassignedDescription);

    /// <summary>The LoopConditionVariableReassigned rule description.</summary>
    private const string LoopConditionVariableReassignedDescription =
        "A for loop's header states how many times it runs: the counter starts, the condition bounds it, and the incrementer steps "
        + "it. When the body also reassigns the counter, or the local the condition compares it against, the loop runs a different "
        + "number of times than the header claims — often forever, or not at all — and the defect hides because the header still "
        + "reads correctly. Only the unambiguous counted shape is reported: a single relational condition built from locals, a "
        + "counter the incrementer steps and the condition tests, and an assignment ('=', '+=', '-=', '++', '--') to one of those "
        + "variables that runs on every iteration. A write guarded by a branch, a switch, or a nested loop is left alone, because it "
        + "may be an intended early advance the rule cannot disprove; the header's own incrementer is never reported.";
}
