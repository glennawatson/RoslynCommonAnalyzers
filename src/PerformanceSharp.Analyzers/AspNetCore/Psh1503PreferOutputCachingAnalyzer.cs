// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a registration of the legacy response-caching middleware (PSH1503): a call to
/// <c>AddResponseCaching(...)</c> on the response-caching service-collection extensions, or a call to
/// <c>UseResponseCaching(...)</c> on the application-builder extensions. Output caching (.NET 7+) is the
/// recommended, more capable server-side cache, so the diagnostic steers to <c>AddOutputCache()</c> /
/// <c>UseOutputCache()</c> / <c>CacheOutput()</c>.
/// </summary>
/// <remarks>
/// The output-caching API is resolved once per compilation, only after a matching invocation is found,
/// so a project without a candidate does not pay for metadata resolution.
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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AspNetCoreRules.PreferOutputCaching);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var markers = new CachingTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, markers),
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
    /// <param name="markers">The caching types resolved on first demand.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, CachingTypes markers)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = GetInvokedMethodName(invocation.Expression);
        if (methodName is not (AddResponseCachingMethodName or UseResponseCachingMethodName))
        {
            return;
        }

        if (markers.Get() is not [var servicesExtensions, var builderExtensions]
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
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

    /// <summary>Resolves caching types only after an invocation passes the syntax filter.</summary>
    /// <param name="compilation">The compilation whose caching types are resolved.</param>
    private sealed class CachingTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the extensions that declare <c>AddResponseCaching</c>.</summary>
        private const string ResponseCachingServicesExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.ResponseCachingServicesExtensions";

        /// <summary>The metadata name of the extensions that declare <c>UseResponseCaching</c>.</summary>
        private const string ResponseCachingBuilderExtensionsMetadataName = "Microsoft.AspNetCore.Builder.ResponseCachingExtensions";

        /// <summary>The metadata name of the output-caching service extensions, one of the marker types.</summary>
        private const string OutputCacheServiceExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.OutputCacheServiceCollectionExtensions";

        /// <summary>The metadata name of the output-caching options type, the alternate marker.</summary>
        private const string OutputCacheOptionsMetadataName = "Microsoft.AspNetCore.OutputCaching.OutputCacheOptions";

        /// <summary>The resolved extension types, or an empty array when the rule cannot apply.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the cached extension types, including an unavailable result.</summary>
        /// <returns>The extension types, or an empty array when output caching is unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol?[] Get() => _resolved ??= Resolve(compilation);

        /// <summary>Resolves response-caching extensions when the replacement API is available.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The extension types, or an empty array when output caching is unavailable.</returns>
        private static INamedTypeSymbol?[] Resolve(Compilation compilation)
        {
            if (compilation.GetTypeByMetadataName(OutputCacheServiceExtensionsMetadataName) is null
                && compilation.GetTypeByMetadataName(OutputCacheOptionsMetadataName) is null)
            {
                return [];
            }

            var servicesExtensions = compilation.GetTypeByMetadataName(ResponseCachingServicesExtensionsMetadataName);
            var builderExtensions = compilation.GetTypeByMetadataName(ResponseCachingBuilderExtensionsMetadataName);
            return servicesExtensions is null && builderExtensions is null
                ? []
                : [servicesExtensions, builderExtensions];
        }
    }
}
