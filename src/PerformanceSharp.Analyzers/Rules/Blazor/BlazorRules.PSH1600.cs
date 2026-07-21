// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Descriptor for PSH1600 — a delegate that captures the loop variable is rebuilt for every row on
/// every render because it sits inside a <c>for</c>/<c>foreach</c> in a component's render output.
/// </summary>
internal static partial class BlazorRules
{
    /// <summary>PSH1600 — a per-iteration delegate captures the loop variable inside a component render loop.</summary>
    public static readonly DiagnosticDescriptor RenderLoopDelegateAllocation = Create(
        "PSH1600",
        "Delegate captures the loop variable inside a component render loop",
        "This delegate captures the loop variable, so a new closure is allocated for every row on every render; hoist it to a cached delegate, a method group, or a precomputed per-item model",
        RenderLoopDelegateAllocationDescription);

    /// <summary>The PSH1600 rule description.</summary>
    private const string RenderLoopDelegateAllocationDescription =
        "A component rebuilds its render output by running 'BuildRenderTree' on every render. When a lambda (or the delegate handed to an "
        + "event-callback factory) inside a 'for'/'foreach' render loop reads the loop variable, the compiler allocates a fresh closure and "
        + "delegate for each iteration, so a list of N rows costs N closure allocations on every render, and the new delegate identities also "
        + "defeat the render diff so the framework re-attaches every handler. Only a delegate that captures a variable declared by the loop is "
        + "reported: a loop-invariant lambda, a method group, and a delegate hoisted out of the loop are left alone because their identity does "
        + "not change per row. Move the delegate off the per-row path — cache it in a field, precompute a per-item model that owns the handler, or "
        + "bind a method group that does not capture the iteration variable — so one delegate is reused across renders. The rule costs nothing "
        + "outside a Blazor project.";
}
