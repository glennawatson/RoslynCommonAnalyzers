// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2479 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2479 — a delegate that outlives a loop captures the loop's changing variable.</summary>
    public static readonly DiagnosticDescriptor CapturedLoopVariable = Create(
        "SST2479",
        "A delegate that outlives a loop should not capture the loop's changing variable",
        "This delegate captures the loop variable '{0}', which changes on each iteration, but is stored to run after the loop, so every deferred call reads the final value of '{0}'",
        CapturedLoopVariableDescription);

    /// <summary>The CapturedLoopVariable rule description.</summary>
    private const string CapturedLoopVariableDescription =
        "A 'for', 'while', or 'do' loop shares one storage location for the variable it steps through — the "
        + "variable is not copied per iteration. A closure that captures such a variable captures that single "
        + "shared location, so it reads whatever value the variable holds at the moment the closure runs, not the "
        + "value it held when the closure was created. As long as the closure runs in place inside the same "
        + "iteration this makes no difference, but when the closure is stored where it outlives the iteration — "
        + "subscribed to an event, added to a collection, assigned to a field, array element, or property, handed "
        + "to a deferred runner such as 'Task.Run', or produced by 'yield return' — every deferred call reads the "
        + "variable's final value instead of the per-iteration value, so all of the stored delegates behave as one. "
        + "The rule reports only the escaping shapes it can see locally, and stays silent when the delegate is "
        + "invoked in place, when the captured variable never changes across the loop, and for a 'foreach' iteration "
        + "variable, which the language already gives a fresh copy each iteration. The fix is to copy the loop "
        + "variable into a variable declared inside the loop body and capture that copy instead.";
}
