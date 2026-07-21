// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The component render-lifecycle descriptors (SST2708–SST2710).</summary>
internal static partial class FrameworksRules
{
    /// <summary>SST2708 — a component subscribes to an external event in a lifecycle method but never unsubscribes it.</summary>
    public static readonly DiagnosticDescriptor LifecycleEventSubscriptionLeak = Create(
        "SST2708",
        "Unsubscribe a lifecycle event subscription",
        "This subscription to '{0}' in a lifecycle method is never removed, so the event source keeps the component alive",
        LifecycleEventSubscriptionLeakDescription);

    /// <summary>SST2709 — <c>StateHasChanged</c> is requested while the component is being disposed.</summary>
    public static readonly DiagnosticDescriptor StateHasChangedDuringDisposal = Create(
        "SST2709",
        "Do not request a render while disposing",
        "'StateHasChanged' is called while component '{0}' is being disposed, which the renderer no longer supports and throws",
        StateHasChangedDuringDisposalDescription);

    /// <summary>SST2710 — <c>StateHasChanged</c> is called from a timer callback without marshalling onto the dispatcher.</summary>
    public static readonly DiagnosticDescriptor TimerStateHasChangedOffDispatcher = Create(
        "SST2710",
        "Marshal a timer callback's render request onto the dispatcher",
        "'StateHasChanged' is called directly from a timer callback; call it through InvokeAsync(StateHasChanged) so it runs on the renderer's dispatcher",
        TimerStateHasChangedOffDispatcherDescription);

    /// <summary>The LifecycleEventSubscriptionLeak rule description.</summary>
    private const string LifecycleEventSubscriptionLeakDescription =
        "A component that hooks a .NET event with += inside a lifecycle method (OnInitialized, OnParametersSet, OnAfterRender and "
        + "their async forms) hands the event source a delegate that closes over the component. That delegate roots the whole "
        + "component graph in the source for as long as the source lives — and the source, an injected service or a static, usually "
        + "outlives many components. Nothing collects the component: on a server-rendered circuit every render or navigation leaks "
        + "one more instance, and memory climbs for the life of the source. This complements the disposable-field rules, which watch "
        + "fields a type creates and forgets to dispose; it watches the event handoff those rules do not see. Remove the subscription "
        + "with a matching -= for the same event, normally from Dispose or DisposeAsync, so the source stops holding the component.";

    /// <summary>The StateHasChangedDuringDisposal rule description.</summary>
    private const string StateHasChangedDuringDisposalDescription =
        "Disposal runs while the renderer is tearing the component down: its place in the render tree is already gone. Asking for a "
        + "render at that point — calling StateHasChanged from Dispose or DisposeAsync — has nothing to render into, so the renderer "
        + "rejects it and throws. The call reads as a harmless refresh but faults at runtime every time the component is disposed. "
        + "Drop the call; a component that is going away does not need to re-render.";

    /// <summary>The TimerStateHasChangedOffDispatcher rule description.</summary>
    private const string TimerStateHasChangedOffDispatcherDescription =
        "A System.Threading.Timer or System.Timers.Timer fires its callback on a thread-pool thread, not on the renderer's "
        + "synchronization context. Touching component state from there — calling StateHasChanged directly — races the renderer, and "
        + "the renderer guards against exactly that: it throws when a render is requested off its own dispatcher. Route the call "
        + "through InvokeAsync(StateHasChanged), which marshals the work back onto the dispatcher before the render is requested.";
}
