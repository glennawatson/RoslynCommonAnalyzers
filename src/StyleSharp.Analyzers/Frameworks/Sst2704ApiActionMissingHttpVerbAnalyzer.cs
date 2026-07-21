// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a public action on an <c>[ApiController]</c> type that declares no HTTP verb (SST2704). A public
/// instance method on a controller is treated as an action; when it carries no attribute deriving from
/// <c>Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute</c> (and nothing else that supplies the verbs) it
/// answers every HTTP method, which broadens the surface and can make routing ambiguous against a sibling action
/// that does declare a verb. A method marked <c>[NonAction]</c>, one that already declares a verb, a static
/// method, a property accessor, and an override inherited from <c>object</c> are all left alone. The rule is
/// scoped to <c>[ApiController]</c> types to keep false positives low, is gated on the ASP.NET Core MVC types
/// resolving in the referenced framework so a non-web project pays nothing, and has no code fix because the
/// intended verb cannot be inferred.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2704ApiActionMissingHttpVerbAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the attribute that marks a controller as an API controller.</summary>
    private const string ApiControllerAttributeMetadataName = "Microsoft.AspNetCore.Mvc.ApiControllerAttribute";

    /// <summary>The metadata name of the MVC controller base type.</summary>
    private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    /// <summary>The metadata name of the base attribute the HTTP-verb attributes derive from.</summary>
    private const string HttpMethodAttributeMetadataName = "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute";

    /// <summary>The metadata name of the interface any attribute that supplies HTTP verbs implements.</summary>
    private const string ActionHttpMethodProviderMetadataName = "Microsoft.AspNetCore.Mvc.Routing.IActionHttpMethodProvider";

    /// <summary>The metadata name of the attribute that opts a method out of action discovery.</summary>
    private const string NonActionAttributeMetadataName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.ApiActionMissingHttpVerb);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var apiControllerAttribute = start.Compilation.GetTypeByMetadataName(ApiControllerAttributeMetadataName);
            var controllerBase = start.Compilation.GetTypeByMetadataName(ControllerBaseMetadataName);
            var httpMethodAttribute = start.Compilation.GetTypeByMetadataName(HttpMethodAttributeMetadataName);
            if (apiControllerAttribute is null || controllerBase is null || httpMethodAttribute is null)
            {
                return;
            }

            var actionHttpMethodProvider = start.Compilation.GetTypeByMetadataName(ActionHttpMethodProviderMetadataName);
            var nonActionAttribute = start.Compilation.GetTypeByMetadataName(NonActionAttributeMetadataName);
            var markers = new MvcMarkers(apiControllerAttribute, controllerBase, httpMethodAttribute, actionHttpMethodProvider, nonActionAttribute);
            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, markers), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports SST2704 for each verb-less public action on an <c>[ApiController]</c> type.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="markers">The resolved MVC marker types the rule gates on.</param>
    private static void AnalyzeType(SymbolAnalysisContext context, MvcMarkers markers)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class
            || !HasApiControllerAttribute(type, markers.ApiControllerAttribute)
            || !IsOrDerivesFrom(type, markers.ControllerBase))
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol method && IsVerblessAction(method, markers))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    FrameworksRules.ApiActionMissingHttpVerb,
                    method.Locations[0],
                    method.Name));
            }
        }
    }

    /// <summary>Returns whether a method is a public action that declares no HTTP verb and is not opted out.</summary>
    /// <param name="method">The candidate method.</param>
    /// <param name="markers">The resolved MVC marker types.</param>
    /// <returns><see langword="true"/> when the method answers every verb and should declare one.</returns>
    private static bool IsVerblessAction(IMethodSymbol method, MvcMarkers markers)
        => IsActionCandidate(method) && !DeclaresVerbOrOptsOut(method, markers);

    /// <summary>Returns whether a method has the shape of a routable action (before attributes are considered).</summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for a public, non-static, non-generic, ordinary method that is not an <c>object</c> override.</returns>
    private static bool IsActionCandidate(IMethodSymbol method)
        => method.DeclaredAccessibility == Accessibility.Public
            && !method.IsStatic
            && !method.IsAbstract
            && !method.IsGenericMethod
            && method.MethodKind == MethodKind.Ordinary
            && method.Locations.Length > 0
            && !OverridesObjectMethod(method);

    /// <summary>Returns whether a method already declares its HTTP verbs or opts out of action discovery.</summary>
    /// <param name="method">The candidate method.</param>
    /// <param name="markers">The resolved MVC marker types.</param>
    /// <returns><see langword="true"/> when a verb attribute or <c>[NonAction]</c> is present.</returns>
    private static bool DeclaresVerbOrOptsOut(IMethodSymbol method, MvcMarkers markers)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            if (SuppliesHttpVerb(attributeClass, markers)
                || (markers.NonActionAttribute is not null && IsOrDerivesFrom(attributeClass, markers.NonActionAttribute)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute type supplies the HTTP verbs for an action.</summary>
    /// <param name="attributeClass">The applied attribute's type.</param>
    /// <param name="markers">The resolved MVC marker types.</param>
    /// <returns><see langword="true"/> when the attribute derives from the verb base or implements the verb-provider interface.</returns>
    private static bool SuppliesHttpVerb(INamedTypeSymbol attributeClass, MvcMarkers markers)
    {
        if (IsOrDerivesFrom(attributeClass, markers.HttpMethodAttribute))
        {
            return true;
        }

        if (markers.ActionHttpMethodProvider is null)
        {
            return false;
        }

        foreach (var implemented in attributeClass.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented, markers.ActionHttpMethodProvider))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method overrides a member that is ultimately declared on <c>object</c>.</summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for an override of <c>ToString</c>, <c>Equals</c>, <c>GetHashCode</c>, and the like.</returns>
    private static bool OverridesObjectMethod(IMethodSymbol method)
    {
        if (!method.IsOverride)
        {
            return false;
        }

        var root = method;
        while (root.OverriddenMethod is { } overridden)
        {
            root = overridden;
        }

        return root.ContainingType?.SpecialType == SpecialType.System_Object;
    }

    /// <summary>Returns whether a type carries the <c>[ApiController]</c> attribute on itself or a base type.</summary>
    /// <param name="type">The candidate controller type.</param>
    /// <param name="apiControllerAttribute">The resolved <c>ApiControllerAttribute</c> type.</param>
    /// <returns><see langword="true"/> when the attribute is present anywhere in the type's hierarchy.</returns>
    private static bool HasApiControllerAttribute(INamedTypeSymbol type, INamedTypeSymbol apiControllerAttribute)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass is { } attributeClass && IsOrDerivesFrom(attributeClass, apiControllerAttribute))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is, or derives from, the supplied base type.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="baseType">The base type to test against.</param>
    /// <returns><see langword="true"/> when the type is the base type or a subclass of it.</returns>
    private static bool IsOrDerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The resolved MVC marker types carried through the per-type analysis.</summary>
    /// <param name="ApiControllerAttribute">The resolved <c>ApiControllerAttribute</c> type.</param>
    /// <param name="ControllerBase">The resolved <c>ControllerBase</c> type.</param>
    /// <param name="HttpMethodAttribute">The resolved <c>HttpMethodAttribute</c> base type.</param>
    /// <param name="ActionHttpMethodProvider">The resolved <c>IActionHttpMethodProvider</c> interface, or <see langword="null"/> when absent.</param>
    /// <param name="NonActionAttribute">The resolved <c>NonActionAttribute</c> type, or <see langword="null"/> when absent.</param>
    private readonly record struct MvcMarkers(
        INamedTypeSymbol ApiControllerAttribute,
        INamedTypeSymbol ControllerBase,
        INamedTypeSymbol HttpMethodAttribute,
        INamedTypeSymbol? ActionHttpMethodProvider,
        INamedTypeSymbol? NonActionAttribute);
}
