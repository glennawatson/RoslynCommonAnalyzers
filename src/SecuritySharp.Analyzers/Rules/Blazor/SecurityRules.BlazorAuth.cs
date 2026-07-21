// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The Blazor authorization descriptors (SES1703, SES1704).</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1703 — <c>[Authorize]</c> on a component that is not routable enforces nothing.</summary>
    public static readonly DiagnosticDescriptor NonRoutableComponentAuthorization = Create(
        "SES1703",
        "Authorize on a non-routable component enforces nothing",
        NonRoutableComponentAuthorizationMessage,
        Blazor,
        NonRoutableComponentAuthorizationDescription);

    /// <summary>SES1704 — an interactive component captures <c>HttpContext</c>, which is null or stale there.</summary>
    public static readonly DiagnosticDescriptor InteractiveComponentHttpContext = Create(
        "SES1704",
        "HttpContext accessed from an interactive component is stale",
        InteractiveComponentHttpContextMessage,
        Blazor,
        InteractiveComponentHttpContextDescription);

    /// <summary>The SES1703 message format.</summary>
    private const string NonRoutableComponentAuthorizationMessage =
        "The '[Authorize]' on this component has no effect: authorization runs as a routing concern and this "
        + "component is not routable (it has no '@page'/'[Route]'), so nothing enforces the attribute";

    /// <summary>The SES1704 message format.</summary>
    private const string InteractiveComponentHttpContextMessage =
        "This '{0}' is captured under an interactive render mode, where HttpContext is null or frozen at circuit "
        + "start and never refreshed, so reading request or authorization state through it is unreliable";

    /// <summary>The SES1703 rule description.</summary>
    private const string NonRoutableComponentAuthorizationDescription =
        "In Blazor, authorization is evaluated by the router when it resolves a routable page: the router reads the "
        + "page's '[Authorize]' before it renders the page. A component that carries '[Authorize]' but no '[Route]' "
        + "(the '@page' directive) is never reached through routing, so its '[Authorize]' is never evaluated -- the "
        + "component renders whenever a parent decides to render it, regardless of the policy. The marker reads as "
        + "protection but enforces nothing, which gives a false sense of security. Put the authorization where it is "
        + "enforced: on the routable page that hosts this component, or in an '<AuthorizeView>' around the sensitive "
        + "content, then drop the '[Authorize]' from the child component. Abstract components and layout components "
        + "(deriving from 'LayoutComponentBase') are exempt because they are base types rather than the rendered "
        + "leaf. Additional exempt type names can be listed in 'securitysharp.SES1703.exempt_types'.";

    /// <summary>The SES1704 rule description.</summary>
    private const string InteractiveComponentHttpContextDescription =
        "A component whose definition fixes an interactive render mode (Interactive Server, Interactive WebAssembly, "
        + "or Interactive Auto) does not execute inside a live HTTP request. 'IHttpContextAccessor' returns null "
        + "there, and an 'HttpContext' captured as a '[CascadingParameter]' is frozen at the moment the circuit "
        + "started: its user, claims, and headers are never refreshed as the circuit lives on. Code that reads "
        + "authorization or request state through either one is therefore acting on absent or stale data, which is a "
        + "security bug when it gates access. Flow the specific values the component needs -- the authenticated user "
        + "through 'AuthenticationStateProvider', a claim, or a header value -- in as parameters instead of capturing "
        + "'HttpContext'. The render mode must be declared on the component definition (an '@rendermode' on the "
        + "component itself, which the compiler turns into a 'RenderModeAttribute' on the type) for this to be "
        + "decidable; a render mode applied only where the component is used is not visible on the definition and is "
        + "not reported.";
}
