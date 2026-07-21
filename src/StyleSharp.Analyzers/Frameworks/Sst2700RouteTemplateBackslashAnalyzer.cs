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
/// types resolving in the referenced framework, so a non-web project registers nothing and pays nothing. A code
/// fix replaces each backslash with a forward slash.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2700RouteTemplateBackslashAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the attribute that carries an explicit route template.</summary>
    private const string RouteAttributeMetadataName = "Microsoft.AspNetCore.Mvc.RouteAttribute";

    /// <summary>The metadata name of the base attribute the HTTP-verb attributes derive from.</summary>
    private const string HttpMethodAttributeMetadataName = "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute";

    /// <summary>The constructor parameter name that carries the route template on the routing attributes.</summary>
    private const string TemplateParameterName = "template";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.RouteTemplateBackslash);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var routeAttribute = start.Compilation.GetTypeByMetadataName(RouteAttributeMetadataName);
            if (routeAttribute is null)
            {
                return;
            }

            var httpMethodAttribute = start.Compilation.GetTypeByMetadataName(HttpMethodAttributeMetadataName);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAttribute(nodeContext, routeAttribute, httpMethodAttribute),
                SyntaxKind.Attribute);
        });
    }

    /// <summary>Reports SST2700 for a routing attribute whose route-template argument contains a backslash.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="routeAttribute">The resolved <c>RouteAttribute</c> type.</param>
    /// <param name="httpMethodAttribute">The resolved <c>HttpMethodAttribute</c> base type, or <see langword="null"/> when absent.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol routeAttribute, INamedTypeSymbol? httpMethodAttribute)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || !HasBackslashStringArgument(argumentList))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !IsRoutingAttribute(constructor.ContainingType, routeAttribute, httpMethodAttribute))
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

    /// <summary>Returns whether any constructor string-literal argument's decoded value contains a backslash.</summary>
    /// <param name="argumentList">The attribute's argument list.</param>
    /// <returns><see langword="true"/> when a backslash-bearing string literal is present, so binding is worthwhile.</returns>
    private static bool HasBackslashStringArgument(AttributeArgumentListSyntax argumentList)
    {
        foreach (var argument in argumentList.Arguments)
        {
            if (argument.NameEquals is null && IsBackslashStringLiteral(argument.Expression))
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
            if (argument.NameEquals is not null)
            {
                // A 'Name = "..."' style property initializer is not a constructor argument, so it is never the template.
                continue;
            }

            string? parameterName;
            if (argument.NameColon is { } nameColon)
            {
                parameterName = nameColon.Name.Identifier.ValueText;
            }
            else
            {
                parameterName = positional < constructor.Parameters.Length ? constructor.Parameters[positional].Name : null;
                positional++;
            }

            if (string.Equals(parameterName, TemplateParameterName, StringComparison.Ordinal)
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
    private static bool IsBackslashStringLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
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
