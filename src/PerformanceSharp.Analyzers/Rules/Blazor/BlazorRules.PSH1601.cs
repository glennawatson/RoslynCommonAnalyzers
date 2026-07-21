// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Descriptor for PSH1601 — a JavaScript-interop call is issued once per iteration inside a
/// <c>for</c>/<c>foreach</c>, so a collection of N items costs N separate calls.
/// </summary>
internal static partial class BlazorRules
{
    /// <summary>PSH1601 — a per-iteration JavaScript-interop call turns N items into N SignalR round-trips.</summary>
    public static readonly DiagnosticDescriptor JsInteropInLoop = Create(
        "PSH1601",
        "JavaScript-interop call issued once per loop iteration",
        "This JavaScript-interop call runs once per loop iteration, so on Interactive Server each item is a separate SignalR round-trip; batch the whole collection into a single interop call",
        JsInteropInLoopDescription);

    /// <summary>The PSH1601 rule description.</summary>
    private const string JsInteropInLoopDescription =
        "An 'IJSRuntime' or 'IJSObjectReference' 'InvokeAsync'/'InvokeVoidAsync' call placed directly inside a 'for'/'foreach' is "
        + "executed once per item. Under the Interactive Server hosting model the component runs on the server and every interop "
        + "call is marshalled to the browser over the SignalR circuit, so a loop of N items becomes N separate network round-trips: "
        + "the latency is paid N times and the circuit is saturated with chatter. Move the interop out of the loop — pass the whole "
        + "collection to a single JavaScript function that does the per-item work on the client, so one round-trip replaces the N. "
        + "Only a call written directly in the loop body is reported; a call nested inside a lambda or local function is left to its "
        + "own analysis. The rule is gated on 'Microsoft.JSInterop.IJSRuntime' and costs nothing outside a Blazor project.";
}
