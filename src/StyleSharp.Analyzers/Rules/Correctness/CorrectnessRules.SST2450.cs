// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2450 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2450 — a Debug.Assert condition contains an expression with a side effect.</summary>
    public static readonly DiagnosticDescriptor DebugAssertConditionSideEffect = Create(
        "SST2450",
        "Debug.Assert conditions must not have side effects",
        "The condition contains {0}; release builds omit the whole Debug.Assert call, so this side effect happens only in debug builds",
        DebugAssertConditionSideEffectDescription);

    /// <summary>The DebugAssertConditionSideEffect rule description.</summary>
    private const string DebugAssertConditionSideEffectDescription =
        "Debug.Assert carries [Conditional(\"DEBUG\")], so a release build omits the whole call and never evaluates the condition. A "
        + "condition that assigns a variable, increments or decrements one, or calls a state-changing collection or enumerator method "
        + "therefore does that work in debug builds only, and the program behaves differently once the asserts are compiled away. Do the "
        + "work in its own statement, which runs in every build, and assert the result.";
}
