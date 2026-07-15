// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2411 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2411 — a for loop declares and tests a counter it never advances.</summary>
    public static readonly DiagnosticDescriptor LoopCounterNeverStepped = Create(
        "SST2411",
        "A for loop's counter should be advanced",
        "The 'for' loop declares '{0}' and tests it, but never advances it",
        LoopCounterNeverSteppedDescription);

    /// <summary>The LoopCounterNeverStepped rule description.</summary>
    private const string LoopCounterNeverSteppedDescription =
        "A 'for' loop introduces a counter, reads it in the stop condition, and then neither the incrementer nor the body ever writes "
        + "to it. The counter cannot move, so the condition keeps the answer it started with — the loop runs forever or not at all. The "
        + "usual cause is a forgotten step, or a step that was written against the wrong variable. Where the counter only walks a "
        + "collection, a 'foreach', a 'for' over a span, or a range makes the intent explicit and removes the counter that was left "
        + "unadvanced.";
}
