// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Descriptor for PSH1602 — <c>StateHasChanged()</c> is called on the unconditional path of
/// <c>OnAfterRender</c>/<c>OnAfterRenderAsync</c>, so it schedules another render after every render.
/// </summary>
internal static partial class BlazorRules
{
    /// <summary>PSH1602 — an unguarded 'StateHasChanged()' in a render callback spins a render loop.</summary>
    public static readonly DiagnosticDescriptor UnconditionalStateHasChanged = Create(
        "PSH1602",
        "StateHasChanged is called unconditionally in a render callback",
        UnconditionalStateHasChangedMessage,
        UnconditionalStateHasChangedDescription);

    /// <summary>The PSH1602 rule message.</summary>
    private const string UnconditionalStateHasChangedMessage =
        "This 'StateHasChanged()' runs on every render because it is not guarded by the 'firstRender' parameter or a state flag, "
        + "so it schedules another render after each render and spins a render loop; guard it with 'if (firstRender)' or a boolean flag";

    /// <summary>The PSH1602 rule description.</summary>
    private const string UnconditionalStateHasChangedDescription =
        "A component's 'OnAfterRender'/'OnAfterRenderAsync' runs after every render pass. Calling 'StateHasChanged()' on its "
        + "unconditional path requests yet another render, whose 'OnAfterRender' calls 'StateHasChanged()' again — a render loop "
        + "that pins the CPU and, on Interactive Server, floods the SignalR circuit. The callback receives a 'firstRender' flag for "
        + "exactly this reason: post-render work that must run once belongs behind 'if (firstRender)', and work that must run when "
        + "some state actually changed belongs behind that state's own guard. Only a 'StateHasChanged()' that the render callback "
        + "reaches unconditionally is reported; a call already inside an 'if' — whether guarded by 'firstRender' or a boolean flag — "
        + "is left alone, and so is a call deferred into a lambda or local function. The rule is gated on "
        + "'Microsoft.AspNetCore.Components.ComponentBase' and costs nothing outside a Blazor project.";
}
