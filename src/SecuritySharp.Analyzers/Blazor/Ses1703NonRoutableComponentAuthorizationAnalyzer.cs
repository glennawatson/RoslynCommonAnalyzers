// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
/// <c>Microsoft.AspNetCore.Components.ComponentBase</c> resolving. Marker resolution waits until a class
/// declaration has attributes and is not explicitly abstract.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1703NonRoutableComponentAuthorizationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The rule-specific exempt-types key.</summary>
    private const string ExemptTypesRuleKey = "securitysharp.SES1703.exempt_types";

    /// <summary>The project-wide exempt-types key.</summary>
    private const string ExemptTypesGeneralKey = "securitysharp.exempt_types";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.NonRoutableComponentAuthorization);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var markers = new Markers(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeType(nodeContext, markers), SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Reports SES1703 when a non-routable component carries <c>[Authorize]</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The compilation's deferred framework markers.</param>
    private static void AnalyzeType(in SyntaxNodeAnalysisContext context, Markers markers)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        // Syntactic prefilter: no attributes means no '[Authorize]' can be present.
        if (declaration.AttributeLists.Count == 0
            || declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || (declaration.BaseList is null && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            return;
        }

        if (markers.Get() is not [var authorize, var componentBase, var route, var layout])
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
        in SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        INamedTypeSymbol authorize,
        INamedTypeSymbol route)
    {
        AttributeSyntax? authorizeAttribute = null;
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attributeType = BlazorComponentHelper.GetAttributeType(context.SemanticModel, attributes[j], context.CancellationToken);
                if (BlazorComponentHelper.IsOrDerivesFrom(attributeType, route))
                {
                    return null;
                }

                if (authorizeAttribute is null && BlazorComponentHelper.IsOrDerivesFrom(attributeType, authorize))
                {
                    authorizeAttribute = attributes[j];
                }
            }
        }

        return authorizeAttribute;
    }

    /// <summary>Returns whether a declaration is a concrete, non-exempt component the rule should report.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="componentBase">The resolved <c>ComponentBase</c> type.</param>
    /// <param name="layout">The resolved <c>LayoutComponentBase</c> type.</param>
    /// <returns><see langword="true"/> when the type is a component and is not abstract, a layout, or exempt.</returns>
    private static bool IsReportableComponent(
        in SyntaxNodeAnalysisContext context,
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
    private static bool IsExemptType(in SyntaxNodeAnalysisContext context, SyntaxTree tree, INamedTypeSymbol typeSymbol)
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

    /// <summary>Resolves the framework markers once per compilation, on first demand.</summary>
    /// <param name="compilation">The compilation whose framework markers are resolved.</param>
    private sealed class Markers(Compilation compilation)
    {
        /// <summary>The metadata name of the marker whose presence on a non-routable component is reported.</summary>
        private const string AuthorizeMetadataName = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";

        /// <summary>The metadata name of the component base type the rule is scoped to.</summary>
        private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

        /// <summary>The metadata name of the routing marker (the <c>@page</c> directive) that makes a component routable.</summary>
        private const string RouteAttributeMetadataName = "Microsoft.AspNetCore.Components.RouteAttribute";

        /// <summary>The metadata name of the layout base type whose descendants are exempt.</summary>
        private const string LayoutComponentBaseMetadataName = "Microsoft.AspNetCore.Components.LayoutComponentBase";

        /// <summary>Serializes the first metadata lookup while leaving cached reads lock-free.</summary>
        private readonly object _gate = new();

        /// <summary>The resolved markers, or an empty array when a required marker is absent.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Gets the markers, caching missing markers as an empty result.</summary>
        /// <returns>The four required markers, or an empty array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol[] Get() => Volatile.Read(ref _resolved) ?? Resolve();

        /// <summary>Resolves the markers required to identify a non-routable component.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The four required markers, or an empty array when any is absent.</returns>
        private static INamedTypeSymbol[] Collect(Compilation compilation)
        {
            var authorize = compilation.GetTypeByMetadataName(AuthorizeMetadataName);
            var componentBase = compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
            var route = compilation.GetTypeByMetadataName(RouteAttributeMetadataName);
            var layout = compilation.GetTypeByMetadataName(LayoutComponentBaseMetadataName);
            return authorize is not null && componentBase is not null && route is not null && layout is not null
                ? [authorize, componentBase, route, layout]
                : [];
        }

        /// <summary>Resolves and publishes all markers once for concurrent candidate callbacks.</summary>
        /// <returns>The four required markers, or an empty array.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol[] Resolve()
        {
            lock (_gate)
            {
                var resolved = _resolved;
                if (resolved is null)
                {
                    resolved = Collect(compilation);
                    Volatile.Write(ref _resolved, resolved);
                }

                return resolved;
            }
        }
    }
}
