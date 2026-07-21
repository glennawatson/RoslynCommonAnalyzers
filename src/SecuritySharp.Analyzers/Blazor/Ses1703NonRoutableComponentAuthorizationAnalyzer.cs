// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a Blazor component that carries <c>[Authorize]</c> but is not routable (SES1703). Blazor evaluates
/// authorization as a routing concern: the router checks a page's <c>[Authorize]</c> when it resolves the
/// route. A component that derives from <c>ComponentBase</c> and carries <c>[Authorize]</c> yet has no
/// <c>[Route]</c> (the <c>@page</c> directive) is never reached through routing, so the <c>[Authorize]</c> is
/// never enforced -- it reads as protection but locks down nothing. The rule reports the misleading
/// <c>[Authorize]</c> attribute. It is purely local: the two markers are matched on the analyzed declaration
/// itself, bound by attribute class (a subclass of <c>AuthorizeAttribute</c> or <c>RouteAttribute</c> counts),
/// never by written name. Abstract types and layout components (deriving from <c>LayoutComponentBase</c>) are
/// exempt, as are any type names listed in <c>securitysharp.SES1703.exempt_types</c> /
/// <c>securitysharp.exempt_types</c>. The whole rule is gated on
/// <c>Microsoft.AspNetCore.Authorization.AuthorizeAttribute</c> and
/// <c>Microsoft.AspNetCore.Components.ComponentBase</c> resolving; a project without Blazor components pays
/// nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1703NonRoutableComponentAuthorizationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the marker whose presence on a non-routable component is reported.</summary>
    private const string AuthorizeMetadataName = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";

    /// <summary>The metadata name of the component base type the rule is scoped to.</summary>
    private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

    /// <summary>The metadata name of the routing marker (the <c>@page</c> directive) that makes a component routable.</summary>
    private const string RouteAttributeMetadataName = "Microsoft.AspNetCore.Components.RouteAttribute";

    /// <summary>The metadata name of the layout base type whose descendants are exempt.</summary>
    private const string LayoutComponentBaseMetadataName = "Microsoft.AspNetCore.Components.LayoutComponentBase";

    /// <summary>The rule-specific exempt-types key.</summary>
    private const string ExemptTypesRuleKey = "securitysharp.SES1703.exempt_types";

    /// <summary>The project-wide exempt-types key.</summary>
    private const string ExemptTypesGeneralKey = "securitysharp.exempt_types";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NonRoutableComponentAuthorization);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var authorize = start.Compilation.GetTypeByMetadataName(AuthorizeMetadataName);
            var componentBase = start.Compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
            var route = start.Compilation.GetTypeByMetadataName(RouteAttributeMetadataName);
            var layout = start.Compilation.GetTypeByMetadataName(LayoutComponentBaseMetadataName);

            // The routing and layout markers ship in the same assembly as ComponentBase, so a project with
            // Blazor components resolves all four; a project without them registers nothing and pays nothing.
            if (authorize is null || componentBase is null || route is null || layout is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeType(nodeContext, authorize, componentBase, route, layout),
                SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Reports SES1703 when a non-routable component carries <c>[Authorize]</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="authorize">The resolved <c>AuthorizeAttribute</c> type.</param>
    /// <param name="componentBase">The resolved <c>ComponentBase</c> type.</param>
    /// <param name="route">The resolved <c>RouteAttribute</c> type.</param>
    /// <param name="layout">The resolved <c>LayoutComponentBase</c> type.</param>
    private static void AnalyzeType(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol authorize,
        INamedTypeSymbol componentBase,
        INamedTypeSymbol route,
        INamedTypeSymbol layout)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        // Syntactic prefilter: no attributes means no '[Authorize]' can be present.
        if (declaration.AttributeLists.Count == 0)
        {
            return;
        }

        // A routable page with '[Authorize]' is the correct pattern; only the non-routable shape is a candidate.
        if (FindNonRoutableAuthorize(context, declaration.AttributeLists, authorize, route) is not { } authorizeAttribute)
        {
            return;
        }

        // Only now bind the type: confirm it is a non-exempt component. This runs only for the rare
        // '[Authorize]'-without-'[Route]' shape, so the common path never pays for the type binding.
        if (!IsReportableComponent(context, declaration, componentBase, layout))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonRoutableComponentAuthorization,
            authorizeAttribute.SyntaxTree,
            authorizeAttribute.Span));
    }

    /// <summary>Finds the <c>[Authorize]</c> attribute on a declaration that carries no <c>[Route]</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeLists">The declaration's attribute lists.</param>
    /// <param name="authorize">The resolved <c>AuthorizeAttribute</c> type.</param>
    /// <param name="route">The resolved <c>RouteAttribute</c> type.</param>
    /// <returns>The first <c>[Authorize]</c> attribute when the declaration is not routable; otherwise <see langword="null"/>.</returns>
    private static AttributeSyntax? FindNonRoutableAuthorize(
        SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        INamedTypeSymbol authorize,
        INamedTypeSymbol route)
    {
        AttributeSyntax? authorizeAttribute = null;
        var routable = false;
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attributeType = BlazorComponentHelper.GetAttributeType(context.SemanticModel, attributes[j], context.CancellationToken);
                if (BlazorComponentHelper.IsOrDerivesFrom(attributeType, route))
                {
                    routable = true;
                }
                else if (authorizeAttribute is null && BlazorComponentHelper.IsOrDerivesFrom(attributeType, authorize))
                {
                    authorizeAttribute = attributes[j];
                }
            }
        }

        return routable ? null : authorizeAttribute;
    }

    /// <summary>Returns whether a declaration is a concrete, non-exempt component the rule should report.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="componentBase">The resolved <c>ComponentBase</c> type.</param>
    /// <param name="layout">The resolved <c>LayoutComponentBase</c> type.</param>
    /// <returns><see langword="true"/> when the type is a component and is not abstract, a layout, or exempt.</returns>
    private static bool IsReportableComponent(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol componentBase,
        INamedTypeSymbol layout)
    {
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } typeSymbol
            || !BlazorComponentHelper.IsOrDerivesFrom(typeSymbol, componentBase)
            || typeSymbol.IsAbstract
            || BlazorComponentHelper.IsOrDerivesFrom(typeSymbol, layout))
        {
            return false;
        }

        return !IsExemptType(context, declaration.SyntaxTree, typeSymbol);
    }

    /// <summary>Returns whether a component's type name was configured as exempt.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="tree">The declaration's syntax tree, used to resolve the config options.</param>
    /// <param name="typeSymbol">The component's type symbol.</param>
    /// <returns><see langword="true"/> when the type's simple or fully qualified name is on the exempt list.</returns>
    private static bool IsExemptType(SyntaxNodeAnalysisContext context, SyntaxTree tree, INamedTypeSymbol typeSymbol)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        var exemptTypes = AnalyzerOptionReader.ReadCommaSeparatedList(options, ExemptTypesRuleKey, ExemptTypesGeneralKey);
        if (exemptTypes.Length == 0)
        {
            return false;
        }

        var simpleName = typeSymbol.Name;
        var fullName = typeSymbol.ToDisplayString();
        for (var i = 0; i < exemptTypes.Length; i++)
        {
            var exempt = exemptTypes[i];
            if (string.Equals(exempt, simpleName, StringComparison.Ordinal) || string.Equals(exempt, fullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
