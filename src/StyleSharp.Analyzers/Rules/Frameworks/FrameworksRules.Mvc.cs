// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <content>Descriptors for the ASP.NET Core MVC convention rules: SST2700, SST2704, SST2705.</content>
internal static partial class FrameworksRules
{
    /// <summary>SST2700 — a routing attribute's route template contains a backslash, which never matches a URL path.</summary>
    public static readonly DiagnosticDescriptor RouteTemplateBackslash = Create(
        "SST2700",
        "A route template must not contain a backslash",
        "The route template '{0}' contains a backslash; route segments are separated by '/', so this route is unreachable",
        RouteTemplateBackslashDescription);

    /// <summary>SST2704 — a public action on an [ApiController] type declares no HTTP verb, so it answers every verb.</summary>
    public static readonly DiagnosticDescriptor ApiActionMissingHttpVerb = Create(
        "SST2704",
        "An API controller action should declare an HTTP verb",
        "Action '{0}' has no HTTP-verb attribute, so it responds to every HTTP method and can make routing ambiguous",
        ApiActionMissingHttpVerbDescription);

    /// <summary>SST2705 — a bound model exposes a non-nullable value member with no required marker, so a missing field binds to its default.</summary>
    public static readonly DiagnosticDescriptor UnderpostedModelMember = CreateDisabled(
        "SST2705",
        "A bound model member should be marked required or made nullable",
        "'{0}' is a non-nullable value type with no required marker; a request that omits it binds it to its default value with no error",
        UnderpostedModelMemberDescription);

    /// <summary>The SST2700 rule description.</summary>
    private const string RouteTemplateBackslashDescription =
        "A routing attribute — '[HttpGet]', '[HttpPost]', and the other verb attributes that derive from "
        + "'Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute', or '[Route]' — carries a URL route template, and URL paths are "
        + "separated by the forward slash '/'. A backslash '\\' in the template is a mistaken path separator: ASP.NET Core routing "
        + "treats it as a literal character, not a segment boundary, so the endpoint is never matched by the intended request and the "
        + "action becomes unreachable. The template value is checked, so an escaped '\\\\', a verbatim '@\"...\"', or a raw string "
        + "literal are all reported. The rule binds the attribute and only reports the argument that maps to the route-template "
        + "parameter, and it is gated on the ASP.NET Core routing types existing in the referenced framework so a non-web project "
        + "reports nothing. The code fix replaces each backslash with a forward slash.";

    /// <summary>The SST2704 rule description.</summary>
    private const string ApiActionMissingHttpVerbDescription =
        "A public instance method declared on a type annotated '[ApiController]' is treated as an action. When that action carries no "
        + "HTTP-verb attribute (none deriving from 'Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute', such as '[HttpGet]' or "
        + "'[HttpPost]', and no attribute that otherwise supplies the verbs) it is reachable through every HTTP method. That is rarely "
        + "intended: it broadens the surface, defeats method-specific routing, and can produce an ambiguous match against a sibling "
        + "action that does declare a verb. A method marked '[NonAction]', a method that already declares a verb, a static method, a "
        + "property accessor, and an override inherited from 'object' are all left alone. The rule is scoped to '[ApiController]' types "
        + "to keep false positives low, and there is no code fix because the intended verb cannot be inferred.";

    /// <summary>The SST2705 rule description.</summary>
    private const string UnderpostedModelMemberDescription =
        "A model bound from the request body of an '[ApiController]' action fills its members from the incoming payload. A public "
        + "settable property or public field whose type is a non-nullable value type — 'int', 'bool', 'decimal', an enum, and so on — "
        + "cannot represent 'absent': when the request omits the field, model binding silently leaves it at its default ('0', 'false'), "
        + "and the action cannot tell a supplied zero from a missing value (an under-posting defect). Marking the member with "
        + "'[Required]' or '[BindRequired]' turns the omission into a validation error, and making the member nullable lets the code "
        + "detect the absence explicitly. Because a default value is often exactly what the API intends, this shape produces real "
        + "false positives, so the rule is disabled by default and must be opted into. It is scoped to source-declared model classes "
        + "bound directly as a body parameter, and there is no code fix because the right remedy — require it or make it nullable — is "
        + "a judgment call.";
}
