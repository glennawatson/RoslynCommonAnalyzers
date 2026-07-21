// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Descriptor for PSH1603 — a non-delegate allocation is handed to a child component as a parameter
/// value inside a <c>for</c>/<c>foreach</c> render loop, so it allocates per row and forces the child
/// to re-render on every pass. This is the non-delegate sibling of PSH1600.
/// </summary>
internal static partial class BlazorRules
{
    /// <summary>PSH1603 — a per-iteration allocation passed as a component parameter allocates per row and re-renders the child.</summary>
    public static readonly DiagnosticDescriptor RenderLoopParameterAllocation = Create(
        "PSH1603",
        "Allocation used as a component parameter inside a render loop",
        RenderLoopParameterAllocationMessage,
        RenderLoopParameterAllocationDescription);

    /// <summary>The PSH1603 rule message.</summary>
    private const string RenderLoopParameterAllocationMessage =
        "This value is newly allocated for every item on every render and handed to a child component as a parameter, so it allocates "
        + "per row and gives the child a fresh value each pass that forces it to re-render; hoist the value or precompute a per-item model";

    /// <summary>The PSH1603 rule description.</summary>
    private const string RenderLoopParameterAllocationDescription =
        "A component rebuilds its render output by running 'BuildRenderTree' on every render. When a 'new' object, an array or "
        + "collection literal, or a materializing query ('ToList'/'ToArray'/…) is passed as a child component's parameter value "
        + "inside a 'for'/'foreach' render loop, a fresh instance is allocated for every row on every render — so a list of N rows "
        + "costs N allocations each pass. Worse, the child compares the incoming parameter by reference: a brand-new instance every "
        + "render never equals the last one, so the child is treated as changed and re-rendered even when its data did not move. "
        + "Hoist the value so its identity is stable — cache it in a field, or precompute a per-item model once and reuse it across "
        + "renders — so the child sees the same reference and skips the redundant render. Only a non-delegate allocation is reported "
        + "here; a delegate or lambda passed the same way is covered by its own rule. The rule is gated on "
        + "'Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder' and costs nothing outside a Blazor project.";
}
