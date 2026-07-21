// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The Blazor framework descriptors (SST2701–SST2703).</summary>
internal static partial class FrameworksRules
{
    /// <summary>SST2701 — a method the JavaScript runtime is meant to call is not reachable because it is not public.</summary>
    public static readonly DiagnosticDescriptor JSInvokableMustBePublic = Create(
        "SST2701",
        "A [JSInvokable] method must be public",
        "'{0}' is annotated [JSInvokable] but is not public, so JavaScript interop cannot call it",
        JSInvokableMustBePublicDescription);

    /// <summary>SST2702 — a query-string parameter is bound into a property whose type the framework cannot convert to.</summary>
    public static readonly DiagnosticDescriptor SupplyParameterFromQueryUnsupportedType = Create(
        "SST2702",
        "A [SupplyParameterFromQuery] property must be a query-bindable type",
        "'{0}' supplies a query-string parameter of type '{1}', which the framework cannot bind",
        SupplyParameterFromQueryUnsupportedTypeDescription);

    /// <summary>SST2703 — a route template's typed constraint disagrees with the component parameter that receives it.</summary>
    public static readonly DiagnosticDescriptor RouteConstraintTypeMismatch = Create(
        "SST2703",
        "A route constraint must match the component parameter type",
        "Route parameter '{0}' is constrained to '{1}' but the matching component parameter is of type '{2}'",
        RouteConstraintTypeMismatchDescription);

    /// <summary>The JSInvokableMustBePublic rule description.</summary>
    private const string JSInvokableMustBePublicDescription =
        "JavaScript interop reaches a [JSInvokable] method by name, through reflection, across the interop boundary — and it can only "
        + "see a public method. A method marked [JSInvokable] that is private, internal, or protected still compiles and still carries "
        + "the attribute, so it reads as callable, but the runtime never finds it: the JavaScript call fails at the moment it is made, "
        + "not at build time. Make the method public so the interface it advertises is the interface it actually exposes.";

    /// <summary>The SupplyParameterFromQueryUnsupportedType rule description.</summary>
    private const string SupplyParameterFromQueryUnsupportedTypeDescription =
        "A property bound from the query string is filled by parsing text into the property's type, and the framework can only parse a "
        + "fixed set: bool, the numeric primitives, Guid, string, the date and time types, and the Nullable<> and array forms of those. "
        + "A property of any other type — an enum, a custom type, TimeSpan, Uri — compiles cleanly but throws when the component is "
        + "navigated to and the framework tries to bind the value. Expose the value as a supported type and convert it yourself.";

    /// <summary>The RouteConstraintTypeMismatch rule description.</summary>
    private const string RouteConstraintTypeMismatchDescription =
        "A typed route segment such as {id:int} tells the router to accept the segment only when it parses as that type, and to hand the "
        + "parsed value to the component parameter of the same name. When the constraint says one type and the [Parameter] property is "
        + "another — {id:int} feeding a string, {key:guid} feeding an int — the two halves of the same contract disagree, and the value "
        + "either fails to bind or is coerced against the parameter's real type at runtime. Align the constraint and the parameter type.";
}
