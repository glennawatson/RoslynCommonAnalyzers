// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a registration of the legacy response-caching middleware (PSH1503): a call to
/// <c>AddResponseCaching(...)</c> on the response-caching service-collection extensions, or a call to
/// <c>UseResponseCaching(...)</c> on the application-builder extensions. Output caching (.NET 7+) is the
/// recommended, more capable server-side cache, so the diagnostic steers to <c>AddOutputCache()</c> /
/// <c>UseOutputCache()</c> / <c>CacheOutput()</c>.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on the output-caching API resolving, so a project that
/// cannot adopt the suggestion — or is not a web app at all — registers no syntax action and pays nothing.
/// The clean path is a method-name token comparison; only a name-matched invocation is bound, and its
/// containing type must be the response-caching extension class, so a same-named method of your own is
/// never confused with it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1503PreferOutputCachingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The legacy service-registration method name.</summary>
    private const string AddResponseCachingMethodName = "AddResponseCaching";

    /// <summary>The legacy middleware method name.</summary>
    private const string UseResponseCachingMethodName = "UseResponseCaching";

    /// <summary>The output-caching service-registration replacement named in the message.</summary>
    private const string AddOutputCacheSuggestion = "AddOutputCache";

    /// <summary>The output-caching middleware replacement named in the message.</summary>
    private const string UseOutputCacheSuggestion = "UseOutputCache";

    /// <summary>The metadata name of the extensions that declare <c>AddResponseCaching</c>.</summary>
    private const string ResponseCachingServicesExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.ResponseCachingServicesExtensions";

    /// <summary>The metadata name of the extensions that declare <c>UseResponseCaching</c>.</summary>
    private const string ResponseCachingBuilderExtensionsMetadataName = "Microsoft.AspNetCore.Builder.ResponseCachingExtensions";

    /// <summary>The metadata name of the output-caching service extensions, one of the marker types.</summary>
    private const string OutputCacheServiceExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.OutputCacheServiceCollectionExtensions";

    /// <summary>The metadata name of the output-caching options type, the alternate marker.</summary>
    private const string OutputCacheOptionsMetadataName = "Microsoft.AspNetCore.OutputCaching.OutputCacheOptions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AspNetCoreRules.PreferOutputCaching);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(OutputCacheServiceExtensionsMetadataName) is null
                && start.Compilation.GetTypeByMetadataName(OutputCacheOptionsMetadataName) is null)
            {
                return;
            }

            var servicesExtensions = start.Compilation.GetTypeByMetadataName(ResponseCachingServicesExtensionsMetadataName);
            var builderExtensions = start.Compilation.GetTypeByMetadataName(ResponseCachingBuilderExtensionsMetadataName);
            if (servicesExtensions is null && builderExtensions is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, servicesExtensions, builderExtensions),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns the member name an invocation targets, without binding it.</summary>
    /// <param name="expression">The invoked expression.</param>
    /// <returns>The invoked member's simple name, or <see langword="null"/> when the target is not a member access.</returns>
    /// <remarks>
    /// Both methods are extension methods, so a real call always spells the receiver — <c>services.AddResponseCaching()</c>
    /// or <c>app.UseResponseCaching()</c>. An unqualified identifier can never bind to them (an extension method is only
    /// callable in its reduced form), so only the member-access shape is inspected.
    /// </remarks>
    private static string? GetInvokedMethodName(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax memberAccess ? memberAccess.Name.Identifier.ValueText : null;

    /// <summary>Reports PSH1503 for a call that registers the legacy response-caching middleware.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="servicesExtensions">The response-caching service extensions, when referenced.</param>
    /// <param name="builderExtensions">The response-caching application-builder extensions, when referenced.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol? servicesExtensions, INamedTypeSymbol? builderExtensions)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = GetInvokedMethodName(invocation.Expression);
        if (methodName is not (AddResponseCachingMethodName or UseResponseCachingMethodName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || (!SymbolEqualityComparer.Default.Equals(method.ContainingType, servicesExtensions)
                && !SymbolEqualityComparer.Default.Equals(method.ContainingType, builderExtensions)))
        {
            return;
        }

        var replacement = methodName == AddResponseCachingMethodName ? AddOutputCacheSuggestion : UseOutputCacheSuggestion;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            AspNetCoreRules.PreferOutputCaching,
            invocation.SyntaxTree,
            invocation.Span,
            methodName,
            replacement));
    }
}
