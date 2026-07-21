// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The Blazor component-member correctness descriptors (SST2711–SST2713).</summary>
internal static partial class FrameworksRules
{
    /// <summary>SST2711 — a synchronous component lifecycle method is overridden as <c>async void</c>, so the framework never awaits it.</summary>
    public static readonly DiagnosticDescriptor AsyncVoidLifecycleOverride = Create(
        "SST2711",
        "A synchronous component lifecycle override must not be async void",
        "'{0}' overrides a synchronous component lifecycle method as async void, which the framework never awaits; override '{1}' returning Task instead",
        AsyncVoidLifecycleOverrideDescription);

    /// <summary>SST2712 — an injected or cascading component property has no setter, so the framework cannot assign it.</summary>
    public static readonly DiagnosticDescriptor SetterlessInjectedProperty = Create(
        "SST2712",
        "An injected or cascading component property must have a setter",
        "'{0}' is populated by the framework through reflection over settable properties but has no setter, so it is never assigned and stays null",
        SetterlessInjectedPropertyDescription);

    /// <summary>SST2713 — a component-callback reference is created and dropped instead of stored, so nothing ever disposes it.</summary>
    public static readonly DiagnosticDescriptor UnstoredDotNetObjectReference = Create(
        "SST2713",
        "A DotNetObjectReference must be stored so it can be disposed",
        "This DotNetObjectReference is passed inline and never stored, so nothing can dispose it and it leaks on the JavaScript side",
        UnstoredDotNetObjectReferenceDescription);

    /// <summary>The AsyncVoidLifecycleOverride rule description.</summary>
    private const string AsyncVoidLifecycleOverrideDescription =
        "The component runtime calls a synchronous lifecycle method — OnInitialized, OnParametersSet, OnAfterRender — and moves straight on; "
        + "there is no Task for it to await. Declaring one of them async void turns it into fire-and-forget: control returns before the awaited "
        + "work finishes, and any exception thrown after the first await has nowhere to surface. On an interactive server circuit that unobserved "
        + "exception tears the circuit down and drops the whole session. Each of these methods has an ...Async twin that returns Task and that the "
        + "runtime does await; override that twin instead so the work is sequenced and its failures are observed.";

    /// <summary>The SetterlessInjectedProperty rule description.</summary>
    private const string SetterlessInjectedPropertyDescription =
        "A property marked [Inject] or [CascadingParameter] is filled by the runtime assigning it through reflection over the component's settable "
        + "properties. A property with no setter — get-only, or expression-bodied — is invisible to that pass, so the runtime never assigns it and "
        + "it keeps its default value. The member reads as wired up but is null at first use, and the component throws a NullReferenceException the "
        + "moment it touches the dependency. Give the property a setter — a private set keeps it encapsulated — so the runtime can bind it.";

    /// <summary>The UnstoredDotNetObjectReference rule description.</summary>
    private const string UnstoredDotNetObjectReferenceDescription =
        "DotNetObjectReference.Create wraps a .NET object so JavaScript can call back into it, and the runtime holds that wrapper alive in a "
        + "per-circuit map, keyed by id, until the reference is disposed. A reference that is created and passed straight into an interop call, or "
        + "otherwise dropped, is never assigned anywhere the component can reach, so its Dispose is never called and the map entry lives for the "
        + "life of the circuit — the component and everything it captures leak. Store the reference in a field, pass that field to interop, and "
        + "dispose it when the component is disposed.";
}
