// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a backslash in the route template of an ASP.NET Core routing attribute (SST2700). URL paths are
/// separated by the forward slash '/', so a backslash in the template of a verb attribute (one deriving from
/// <c>Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute</c>, such as <c>[HttpGet]</c>/<c>[HttpPost]</c>) or of
/// <c>[Microsoft.AspNetCore.Mvc.RouteAttribute]</c> is a mistaken path separator: routing treats it as a literal
/// character and never matches the intended request, leaving the action unreachable. The attribute is bound and
/// only the argument that maps to the route-template parameter is inspected — its decoded value is checked, so a
/// verbatim, escaped, or raw string literal are all caught. The whole rule is gated on the ASP.NET Core routing
/// types resolving in the referenced framework, checked only after a backslash-bearing literal is found. A
/// code fix replaces each backslash with a forward slash.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2700RouteTemplateBackslashAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The constructor parameter name that carries the route template on the routing attributes.</summary>
    private const string TemplateParameterName = "template";

    /// <summary>The slot holding <c>RouteAttribute</c> in <see cref="RoutingMetadataNames"/>.</summary>
    private const int RouteAttributeSlot = 0;

    /// <summary>The slot holding <c>HttpMethodAttribute</c> in <see cref="RoutingMetadataNames"/>.</summary>
    private const int HttpMethodAttributeSlot = 1;

    /// <summary>The routing attribute metadata names, each resolved once on first demand.</summary>
    private static readonly string[] RoutingMetadataNames =
    [
        "Microsoft.AspNetCore.Mvc.RouteAttribute",
        "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute",
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(FrameworksRules.RouteTemplateBackslash);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypeSlots(compilation, RoutingMetadataNames),
            AnalyzeAttribute,
            SyntaxKind.Attribute);
    }

    /// <summary>Reports SST2700 for a routing attribute whose route-template argument contains a backslash.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="routingTypes">The routing types, each resolved once on first demand.</param>
    private static void AnalyzeAttribute(in SyntaxNodeAnalysisContext context, LazyMetadataTypeSlots routingTypes)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || !HasBackslashStringArgument(argumentList))
        {
            return;
        }

        if (routingTypes.Get(RouteAttributeSlot) is not { } resolvedRoute
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !IsRoutingAttribute(constructor.ContainingType, resolvedRoute, routingTypes.Get(HttpMethodAttributeSlot)))
        {
            return;
        }

        if (FindTemplateLiteral(argumentList, constructor) is not { } literal)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            FrameworksRules.RouteTemplateBackslash,
            literal.SyntaxTree,
            literal.Span,
            literal.Token.ValueText));
    }

    /// <summary>Returns whether a possible template argument's decoded string literal contains a backslash.</summary>
    /// <param name="argumentList">The attribute's argument list.</param>
    /// <returns>True when a positional or explicitly named template argument needs binding.</returns>
    private static bool HasBackslashStringArgument(AttributeArgumentListSyntax argumentList)
    {
        foreach (var argument in argumentList.Arguments)
        {
            if (argument.NameEquals is null
                && (argument.NameColon is null
                    || string.Equals(argument.NameColon.Name.Identifier.ValueText, TemplateParameterName, StringComparison.Ordinal))
                && IsBackslashStringLiteral(argument.Expression))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the route-template literal that carries a backslash, or <see langword="null"/> when none does.</summary>
    /// <param name="argumentList">The attribute's argument list.</param>
    /// <param name="constructor">The bound attribute constructor, used to map positional arguments to parameter names.</param>
    /// <returns>The offending string literal, or <see langword="null"/>.</returns>
    private static LiteralExpressionSyntax? FindTemplateLiteral(AttributeArgumentListSyntax argumentList, IMethodSymbol constructor)
    {
        var positional = 0;
        foreach (var argument in argumentList.Arguments)
        {
            // A 'Name = "..."' style property initializer maps to no constructor parameter, so it is never the template.
            if (string.Equals(AttributeArgumentParameter.NameOf(argument, constructor, ref positional), TemplateParameterName, StringComparison.Ordinal)
                && argument.Expression is LiteralExpressionSyntax literal
                && IsBackslashStringLiteral(literal))
            {
                return literal;
            }
        }

        return null;
    }

    /// <summary>Returns whether an expression is a string literal whose decoded value contains a backslash.</summary>
    /// <param name="expression">The candidate argument expression.</param>
    /// <returns><see langword="true"/> when the expression is a backslash-bearing string literal.</returns>
    private static bool IsBackslashStringLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && literal.Token.ValueText.IndexOf('\\') >= 0;

    /// <summary>Returns whether an attribute type is, or derives from, one of the routing attribute types.</summary>
    /// <param name="attributeType">The bound attribute's type.</param>
    /// <param name="routeAttribute">The resolved <c>RouteAttribute</c> type.</param>
    /// <param name="httpMethodAttribute">The resolved <c>HttpMethodAttribute</c> base type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the attribute participates in URL routing.</returns>
    private static bool IsRoutingAttribute(INamedTypeSymbol attributeType, INamedTypeSymbol routeAttribute, INamedTypeSymbol? httpMethodAttribute)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, routeAttribute)
                || (httpMethodAttribute is not null && SymbolEqualityComparer.Default.Equals(current, httpMethodAttribute)))
            {
                return true;
            }
        }

        return false;
    }
}
