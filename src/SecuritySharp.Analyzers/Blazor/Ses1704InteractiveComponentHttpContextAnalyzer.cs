// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a Blazor component whose definition fixes an interactive render mode and that captures
/// <c>HttpContext</c> (SES1704). Under an interactive render mode the component does not run inside a live
/// HTTP request: <c>IHttpContextAccessor</c> returns null, and an <c>HttpContext</c> taken as a
/// <c>[CascadingParameter]</c> is frozen at circuit start and never refreshed. The rule reports an
/// <c>[Inject]</c> member (or constructor parameter) typed <c>IHttpContextAccessor</c>, and a
/// <c>[CascadingParameter]</c> member typed <c>HttpContext</c>, on a component that carries a
/// <c>RenderModeAttribute</c> on its definition -- the compiler emits that attribute for an <c>@rendermode</c>
/// declared on the component, and the three interactive modes are the only ones that produce it (static
/// server rendering emits none). Detection is decidable only when the render mode is on the definition, so a
/// mode applied where the component is used is not visible here and is not reported. Every marker is bound by
/// symbol; the rule is gated on <c>IHttpContextAccessor</c>, <c>RenderModeAttribute</c>, and
/// <c>ComponentBase</c> resolving, so a project without interactive Blazor components pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1704InteractiveComponentHttpContextAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the injected accessor whose capture the rule reports.</summary>
    private const string HttpContextAccessorMetadataName = "Microsoft.AspNetCore.Http.IHttpContextAccessor";

    /// <summary>The metadata name of the request context type whose cascaded capture the rule reports.</summary>
    private const string HttpContextMetadataName = "Microsoft.AspNetCore.Http.HttpContext";

    /// <summary>The metadata name of the base render-mode attribute that marks an interactive component definition.</summary>
    private const string RenderModeAttributeMetadataName = "Microsoft.AspNetCore.Components.RenderModeAttribute";

    /// <summary>The metadata name of the component base type the rule is scoped to.</summary>
    private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

    /// <summary>The metadata name of the dependency-injection marker on component members.</summary>
    private const string InjectAttributeMetadataName = "Microsoft.AspNetCore.Components.InjectAttribute";

    /// <summary>The metadata name of the cascading-parameter marker on component members.</summary>
    private const string CascadingParameterAttributeMetadataName = "Microsoft.AspNetCore.Components.CascadingParameterAttribute";

    /// <summary>Which <c>HttpContext</c>-capturing marker a component member carries.</summary>
    private enum HttpContextCapture
    {
        /// <summary>The member carries neither marker.</summary>
        None,

        /// <summary>The member is <c>[Inject]</c>ed and is a candidate <c>IHttpContextAccessor</c>.</summary>
        Injected,

        /// <summary>The member is a <c>[CascadingParameter]</c> and is a candidate <c>HttpContext</c>.</summary>
        Cascaded,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.InteractiveComponentHttpContext);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var accessor = start.Compilation.GetTypeByMetadataName(HttpContextAccessorMetadataName);
            var renderMode = start.Compilation.GetTypeByMetadataName(RenderModeAttributeMetadataName);
            var componentBase = start.Compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
            var httpContext = start.Compilation.GetTypeByMetadataName(HttpContextMetadataName);
            var inject = start.Compilation.GetTypeByMetadataName(InjectAttributeMetadataName);
            var cascading = start.Compilation.GetTypeByMetadataName(CascadingParameterAttributeMetadataName);
            if (accessor is null || renderMode is null || componentBase is null || httpContext is null || inject is null || cascading is null)
            {
                return;
            }

            var markers = new BlazorHttpContextMarkers(accessor, httpContext, renderMode, componentBase, inject, cascading);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeType(nodeContext, markers),
                SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Reports SES1704 for each <c>HttpContext</c> capture on an interactive component.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The resolved marker types for the compilation.</param>
    private static void AnalyzeType(SyntaxNodeAnalysisContext context, BlazorHttpContextMarkers markers)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        // Selective prefilter: an interactive render mode is fixed with a class-level attribute, so a
        // component without attributes -- or without a render-mode attribute -- can never match.
        if (declaration.AttributeLists.Count == 0 || !HasRenderModeAttribute(context, declaration.AttributeLists, markers.RenderMode))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } typeSymbol
            || !BlazorComponentHelper.IsOrDerivesFrom(typeSymbol, markers.ComponentBase))
        {
            return;
        }

        AnalyzeMembers(context, declaration, markers);
    }

    /// <summary>Returns whether the declaration carries an attribute deriving from <c>RenderModeAttribute</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeLists">The declaration's attribute lists.</param>
    /// <param name="renderMode">The resolved base <c>RenderModeAttribute</c> type.</param>
    /// <returns><see langword="true"/> when a fixed render mode is declared on the component.</returns>
    private static bool HasRenderModeAttribute(SyntaxNodeAnalysisContext context, SyntaxList<AttributeListSyntax> attributeLists, INamedTypeSymbol renderMode)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attributeType = BlazorComponentHelper.GetAttributeType(context.SemanticModel, attributes[j], context.CancellationToken);
                if (attributeType is not null && BlazorComponentHelper.IsOrDerivesFrom(attributeType, renderMode))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Scans a component's members for an <c>HttpContext</c> capture and reports each one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="declaration">The component declaration.</param>
    /// <param name="markers">The resolved marker types for the compilation.</param>
    private static void AnalyzeMembers(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax declaration, BlazorHttpContextMarkers markers)
    {
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            switch (members[i])
            {
                case PropertyDeclarationSyntax property:
                {
                    AnalyzeAttributedMember(context, property.AttributeLists, property.Type, property.Identifier, markers);
                    break;
                }

                case FieldDeclarationSyntax field:
                {
                    var variables = field.Declaration.Variables;
                    for (var v = 0; v < variables.Count; v++)
                    {
                        AnalyzeAttributedMember(context, field.AttributeLists, field.Declaration.Type, variables[v].Identifier, markers);
                    }

                    break;
                }

                case ConstructorDeclarationSyntax constructor:
                {
                    AnalyzeConstructorParameters(context, constructor, markers.Accessor);
                    break;
                }
            }
        }
    }

    /// <summary>Reports a member marked <c>[Inject]</c>/<c>[CascadingParameter]</c> whose type captures <c>HttpContext</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeLists">The member's attribute lists.</param>
    /// <param name="memberType">The member's declared type syntax.</param>
    /// <param name="identifier">The member's name token, used as the report location.</param>
    /// <param name="markers">The resolved marker types for the compilation.</param>
    private static void AnalyzeAttributedMember(
        SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        TypeSyntax memberType,
        SyntaxToken identifier,
        BlazorHttpContextMarkers markers)
    {
        if (attributeLists.Count == 0)
        {
            return;
        }

        var capture = ClassifyMemberCapture(context, attributeLists, markers.Inject, markers.Cascading);
        if (capture == HttpContextCapture.None)
        {
            return;
        }

        var memberTypeSymbol = context.SemanticModel.GetTypeInfo(memberType, context.CancellationToken).Type;
        var expected = capture == HttpContextCapture.Injected ? markers.Accessor : markers.HttpContext;

        // A null (unresolved) member type simply is not the captured type, so it falls out here.
        if (!SymbolEqualityComparer.Default.Equals(memberTypeSymbol, expected))
        {
            return;
        }

        Report(context, identifier, expected.Name);
    }

    /// <summary>Classifies which <c>HttpContext</c>-capturing marker a member carries, if any.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeLists">The member's attribute lists.</param>
    /// <param name="inject">The resolved <c>InjectAttribute</c> type.</param>
    /// <param name="cascading">The resolved <c>CascadingParameterAttribute</c> type.</param>
    /// <returns>Whether the member is injected, cascaded, or carries neither marker.</returns>
    private static HttpContextCapture ClassifyMemberCapture(
        SyntaxNodeAnalysisContext context,
        SyntaxList<AttributeListSyntax> attributeLists,
        INamedTypeSymbol inject,
        INamedTypeSymbol cascading)
    {
        var injected = false;
        var cascaded = false;
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                var attributeType = BlazorComponentHelper.GetAttributeType(context.SemanticModel, attributes[j], context.CancellationToken);
                if (BlazorComponentHelper.IsOrDerivesFrom(attributeType, inject))
                {
                    injected = true;
                }
                else if (BlazorComponentHelper.IsOrDerivesFrom(attributeType, cascading))
                {
                    cascaded = true;
                }
            }
        }

        if (injected)
        {
            return HttpContextCapture.Injected;
        }

        return cascaded ? HttpContextCapture.Cascaded : HttpContextCapture.None;
    }

    /// <summary>Reports a constructor parameter typed <c>IHttpContextAccessor</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="constructor">The component's constructor.</param>
    /// <param name="accessor">The resolved <c>IHttpContextAccessor</c> type.</param>
    private static void AnalyzeConstructorParameters(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor, INamedTypeSymbol accessor)
    {
        var parameters = constructor.ParameterList.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameterType = context.SemanticModel.GetDeclaredSymbol(parameters[i], context.CancellationToken)?.Type;
            if (SymbolEqualityComparer.Default.Equals(parameterType, accessor))
            {
                Report(context, parameters[i].Identifier, accessor.Name);
            }
        }
    }

    /// <summary>Reports SES1704 at a member name token, naming the captured type in the message.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The member's name token, used as the report location.</param>
    /// <param name="capturedTypeName">The captured type's name for the message.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxToken identifier, string capturedTypeName)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.InteractiveComponentHttpContext,
            identifier.GetLocation(),
            capturedTypeName));

    /// <summary>The resolved marker types SES1704 matches against, resolved once per compilation.</summary>
    /// <param name="Accessor">The <c>IHttpContextAccessor</c> type.</param>
    /// <param name="HttpContext">The <c>HttpContext</c> type.</param>
    /// <param name="RenderMode">The base <c>RenderModeAttribute</c> type.</param>
    /// <param name="ComponentBase">The <c>ComponentBase</c> type.</param>
    /// <param name="Inject">The <c>InjectAttribute</c> type.</param>
    /// <param name="Cascading">The <c>CascadingParameterAttribute</c> type.</param>
    private readonly record struct BlazorHttpContextMarkers(
        INamedTypeSymbol Accessor,
        INamedTypeSymbol HttpContext,
        INamedTypeSymbol RenderMode,
        INamedTypeSymbol ComponentBase,
        INamedTypeSymbol Inject,
        INamedTypeSymbol Cascading);
}
