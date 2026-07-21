// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a non-nullable value-type member on a model bound from the body of an <c>[ApiController]</c> action
/// (SST2705). When a request omits the field, model binding silently leaves such a member at its default
/// (<c>0</c>/<c>false</c>) and the action cannot tell a supplied default from an absent one — an under-posting
/// defect. A public settable property or public field whose type is a non-nullable value type is reported unless
/// it carries <c>[Required]</c> or <c>[BindRequired]</c>; a nullable member can already express "absent" and is
/// left alone. Because a default value is frequently intended, this shape has real false positives and the rule
/// is <b>disabled by default</b> (opt-in through <c>.editorconfig</c>). It is scoped tightly — only source-declared
/// model classes bound directly as a body parameter — is gated on the ASP.NET Core MVC types resolving in the
/// referenced framework, and has no code fix because requiring the member or making it nullable is a judgment call.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2705BoundModelUnderpostingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the attribute that marks a controller as an API controller.</summary>
    private const string ApiControllerAttributeMetadataName = "Microsoft.AspNetCore.Mvc.ApiControllerAttribute";

    /// <summary>The metadata name of the MVC controller base type.</summary>
    private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    /// <summary>The metadata name of the attribute that opts a method out of action discovery.</summary>
    private const string NonActionAttributeMetadataName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";

    /// <summary>The metadata name of the data-annotations required marker.</summary>
    private const string RequiredAttributeMetadataName = "System.ComponentModel.DataAnnotations.RequiredAttribute";

    /// <summary>The metadata name of the model-binding required marker.</summary>
    private const string BindRequiredAttributeMetadataName = "Microsoft.AspNetCore.Mvc.ModelBinding.BindRequiredAttribute";

    /// <summary>The metadata names of the binding-source attributes that route a parameter away from the request body.</summary>
    private static readonly string[] NonBodySourceMetadataNames =
    [
        "Microsoft.AspNetCore.Mvc.FromQueryAttribute",
        "Microsoft.AspNetCore.Mvc.FromRouteAttribute",
        "Microsoft.AspNetCore.Mvc.FromFormAttribute",
        "Microsoft.AspNetCore.Mvc.FromHeaderAttribute",
        "Microsoft.AspNetCore.Mvc.FromServicesAttribute"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.UnderpostedModelMember);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var apiControllerAttribute = start.Compilation.GetTypeByMetadataName(ApiControllerAttributeMetadataName);
            var controllerBase = start.Compilation.GetTypeByMetadataName(ControllerBaseMetadataName);
            if (apiControllerAttribute is null || controllerBase is null)
            {
                return;
            }

            var markers = new BindingMarkers(
                apiControllerAttribute,
                controllerBase,
                start.Compilation.GetTypeByMetadataName(NonActionAttributeMetadataName),
                start.Compilation.GetTypeByMetadataName(RequiredAttributeMetadataName),
                start.Compilation.GetTypeByMetadataName(BindRequiredAttributeMetadataName),
                ResolveNonBodySources(start.Compilation));

            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, markers), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports SST2705 for the under-postable members of every body-bound model on an <c>[ApiController]</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="markers">The resolved MVC and validation marker types.</param>
    private static void AnalyzeType(SymbolAnalysisContext context, BindingMarkers markers)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class
            || !HasApiControllerAttribute(type, markers.ApiControllerAttribute)
            || !IsOrDerivesFrom(type, markers.ControllerBase))
        {
            return;
        }

        var models = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol method || !IsAction(method, markers.NonActionAttribute))
            {
                continue;
            }

            foreach (var parameter in method.Parameters)
            {
                if (TryGetBodyBoundModel(parameter, markers) is { } model)
                {
                    models.Add(model);
                }
            }
        }

        foreach (var model in models)
        {
            ReportUnderpostedMembers(context, model, markers);
        }
    }

    /// <summary>Reports every under-postable public member declared on a body-bound model type.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="model">The body-bound model type.</param>
    /// <param name="markers">The resolved MVC and validation marker types.</param>
    private static void ReportUnderpostedMembers(SymbolAnalysisContext context, INamedTypeSymbol model, BindingMarkers markers)
    {
        foreach (var member in model.GetMembers())
        {
            if (!IsUnderpostableMember(member, markers))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                FrameworksRules.UnderpostedModelMember,
                member.Locations[0],
                member.Name));
        }
    }

    /// <summary>Returns whether a member is a public, settable, non-nullable value member with no required marker.</summary>
    /// <param name="member">The candidate model member.</param>
    /// <param name="markers">The resolved MVC and validation marker types.</param>
    /// <returns><see langword="true"/> when omitting the member from a request silently binds it to its default.</returns>
    private static bool IsUnderpostableMember(ISymbol member, BindingMarkers markers)
        => member switch
        {
            _ when member.Locations.Length == 0 => false,
            IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false, SetMethod.DeclaredAccessibility: Accessibility.Public } property
                => IsNonNullableValueType(property.Type) && !HasRequiredMarker(property, markers),
            IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsConst: false, IsReadOnly: false } field
                => IsNonNullableValueType(field.Type) && !HasRequiredMarker(field, markers),
            _ => false,
        };

    /// <summary>Returns the body-bound model type carried by a parameter, or <see langword="null"/> when it is not one.</summary>
    /// <param name="parameter">The action parameter.</param>
    /// <param name="markers">The resolved MVC and validation marker types.</param>
    /// <returns>The source-declared model class bound from the body, or <see langword="null"/>.</returns>
    private static INamedTypeSymbol? TryGetBodyBoundModel(IParameterSymbol parameter, BindingMarkers markers)
    {
        if (parameter.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class } model
            || model.DeclaringSyntaxReferences.Length == 0
            || ImplementsEnumerable(model)
            || HasNonBodyBindingSource(parameter, markers.NonBodySources))
        {
            return null;
        }

        return model;
    }

    /// <summary>Returns whether a method is a public instance action (verb attributes are not required here).</summary>
    /// <param name="method">The candidate method.</param>
    /// <param name="nonActionAttribute">The resolved <c>NonActionAttribute</c> type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the method's parameters take part in model binding.</returns>
    private static bool IsAction(IMethodSymbol method, INamedTypeSymbol? nonActionAttribute)
        => HasActionShape(method) && !IsNonAction(method, nonActionAttribute);

    /// <summary>Returns whether a method has the shape of an action whose parameters bind (before attributes).</summary>
    /// <param name="method">The candidate method.</param>
    /// <returns><see langword="true"/> for a public, non-static, non-generic, ordinary method with parameters.</returns>
    private static bool HasActionShape(IMethodSymbol method)
        => method.DeclaredAccessibility == Accessibility.Public
            && !method.IsStatic
            && !method.IsAbstract
            && !method.IsGenericMethod
            && method.MethodKind == MethodKind.Ordinary
            && method.Parameters.Length > 0
            && !OverridesObjectMethod(method);

    /// <summary>Returns whether a method is opted out of action discovery with <c>[NonAction]</c>.</summary>
    /// <param name="method">The candidate method.</param>
    /// <param name="nonActionAttribute">The resolved <c>NonActionAttribute</c> type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the method carries <c>[NonAction]</c>.</returns>
    private static bool IsNonAction(IMethodSymbol method, INamedTypeSymbol? nonActionAttribute)
    {
        if (nonActionAttribute is null)
        {
            return false;
        }

        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass && IsOrDerivesFrom(attributeClass, nonActionAttribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a parameter carries a binding-source attribute that routes it away from the body.</summary>
    /// <param name="parameter">The action parameter.</param>
    /// <param name="nonBodySources">The resolved non-body binding-source attribute types.</param>
    /// <returns><see langword="true"/> when the parameter binds from the query, route, form, header, or services.</returns>
    private static bool HasNonBodyBindingSource(IParameterSymbol parameter, INamedTypeSymbol[] nonBodySources)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            for (var i = 0; i < nonBodySources.Length; i++)
            {
                if (IsOrDerivesFrom(attributeClass, nonBodySources[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a member carries a required marker attribute that fails an omitted field.</summary>
    /// <param name="member">The candidate model member.</param>
    /// <param name="markers">The resolved MVC and validation marker types.</param>
    /// <returns><see langword="true"/> when <c>[Required]</c> or <c>[BindRequired]</c> is present.</returns>
    private static bool HasRequiredMarker(ISymbol member, BindingMarkers markers)
    {
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            if ((markers.RequiredAttribute is not null && IsOrDerivesFrom(attributeClass, markers.RequiredAttribute))
                || (markers.BindRequiredAttribute is not null && IsOrDerivesFrom(attributeClass, markers.BindRequiredAttribute)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is a non-nullable value type (a struct, enum, or primitive, but not <c>Nullable&lt;T&gt;</c>).</summary>
    /// <param name="type">The member's type.</param>
    /// <returns><see langword="true"/> when a missing value binds to <c>default</c> with no way to detect the absence.</returns>
    private static bool IsNonNullableValueType(ITypeSymbol type)
        => type.IsValueType
            && type.TypeKind != TypeKind.TypeParameter
            && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;

    /// <summary>Returns whether a type implements <see cref="System.Collections.IEnumerable"/>, marking it a collection.</summary>
    /// <param name="type">The candidate model type.</param>
    /// <returns><see langword="true"/> when the type is a collection rather than a single model.</returns>
    private static bool ImplementsEnumerable(INamedTypeSymbol type)
    {
        foreach (var implemented in type.AllInterfaces)
        {
            if (implemented.SpecialType == SpecialType.System_Collections_IEnumerable)
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

    /// <summary>Resolves the non-body binding-source attribute types present in the compilation.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns>The resolved non-body binding-source attribute types (absent ones are dropped).</returns>
    private static INamedTypeSymbol[] ResolveNonBodySources(Compilation compilation)
    {
        var resolved = new INamedTypeSymbol[NonBodySourceMetadataNames.Length];
        var count = 0;
        for (var i = 0; i < NonBodySourceMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(NonBodySourceMetadataNames[i]) is { } source)
            {
                resolved[count++] = source;
            }
        }

        if (count == NonBodySourceMetadataNames.Length)
        {
            return resolved;
        }

        var trimmed = new INamedTypeSymbol[count];
        System.Array.Copy(resolved, trimmed, count);
        return trimmed;
    }

    /// <summary>The resolved marker types carried through the per-type analysis.</summary>
    /// <param name="ApiControllerAttribute">The resolved <c>ApiControllerAttribute</c> type.</param>
    /// <param name="ControllerBase">The resolved <c>ControllerBase</c> type.</param>
    /// <param name="NonActionAttribute">The resolved <c>NonActionAttribute</c> type, or <see langword="null"/> when absent.</param>
    /// <param name="RequiredAttribute">The resolved <c>RequiredAttribute</c> type, or <see langword="null"/> when absent.</param>
    /// <param name="BindRequiredAttribute">The resolved <c>BindRequiredAttribute</c> type, or <see langword="null"/> when absent.</param>
    /// <param name="NonBodySources">The resolved non-body binding-source attribute types.</param>
    private readonly record struct BindingMarkers(
        INamedTypeSymbol ApiControllerAttribute,
        INamedTypeSymbol ControllerBase,
        INamedTypeSymbol? NonActionAttribute,
        INamedTypeSymbol? RequiredAttribute,
        INamedTypeSymbol? BindRequiredAttribute,
        INamedTypeSymbol[] NonBodySources);
}
