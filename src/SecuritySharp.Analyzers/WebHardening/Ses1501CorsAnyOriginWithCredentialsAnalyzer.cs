// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a single CORS policy that calls both <c>AllowAnyOrigin</c> and <c>AllowCredentials</c> on the same
/// <c>Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder</c> (SES1501). The rule triggers on an
/// <c>AllowCredentials()</c> invocation whose bound method's containing type is <c>CorsPolicyBuilder</c>, then
/// scans the enclosing policy scope -- the configuration lambda body passed to <c>AddPolicy</c>/<c>AddDefaultPolicy</c>,
/// or, for a bare fluent chain, the single enclosing statement -- for an <c>AllowAnyOrigin()</c> call on
/// <c>CorsPolicyBuilder</c>. Both member symbols are bound so a same-named method on an unrelated type is never
/// matched. The scan is a purely local ancestor/descendant walk: no data flow, and cross-statement uses outside a
/// policy lambda are deliberately left alone. <c>CorsPolicyBuilder</c> is resolved only after an
/// <c>AllowCredentials</c> call with an enclosing policy scope passes the syntax checks.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1501CorsAnyOriginWithCredentialsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the credential-allowing fluent method that triggers the rule.</summary>
    private const string AllowCredentialsMethodName = "AllowCredentials";

    /// <summary>The name of the any-origin fluent method whose presence completes the violation.</summary>
    private const string AllowAnyOriginMethodName = "AllowAnyOrigin";

    /// <summary>The metadata name of the CORS policy builder that gates the rule.</summary>
    private const string CorsPolicyBuilderMetadataName = "Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.CorsAnyOriginWithCredentials);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, CorsPolicyBuilderMetadataName),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports SES1501 for an <c>AllowCredentials()</c> call whose policy scope also allows any origin.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="builderTypes">The CORS builder type cache for this compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataType builderTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member-access '.AllowCredentials()' call. The receiver is required, so an
        // unqualified identifier can never reach the instance method and is ignored.
        if (FluentConfigurationScope.GetInvokedName(invocation.Expression) is not { Identifier.ValueText: AllowCredentialsMethodName } credentialsName
            || FluentConfigurationScope.GetScope(invocation) is not { } scope)
        {
            return;
        }

        if (builderTypes.Get() is not { } builderType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: AllowCredentialsMethodName } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType)
            || !FluentConfigurationScope.ContainsBuilderCall(scope, context.SemanticModel, builderType, IsAllowAnyOriginCall, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.CorsAnyOriginWithCredentials,
            invocation.SyntaxTree,
            TextSpan.FromBounds(credentialsName.SpanStart, invocation.Span.End)));
    }

    /// <summary>Returns whether an invocation is an <c>AllowAnyOrigin()</c> call bound to the gated builder.</summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated <c>CorsPolicyBuilder</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for an <c>AllowAnyOrigin()</c> call on the gated builder.</returns>
    private static bool IsAllowAnyOriginCall(InvocationExpressionSyntax invocation, SemanticModel model, INamedTypeSymbol builderType, CancellationToken cancellationToken) =>
        FluentConfigurationScope.GetInvokedName(invocation.Expression) is { Identifier.ValueText: AllowAnyOriginMethodName }
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: AllowAnyOriginMethodName } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType);
}
