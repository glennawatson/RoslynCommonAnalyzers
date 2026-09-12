// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an <c>UseDeveloperExceptionPage</c> call that is not lexically enclosed by a
/// development-environment guard (SES1506). The extension method (on
/// <c>Microsoft.AspNetCore.Builder.DeveloperExceptionPageExtensions</c>, whose first parameter is
/// <c>IApplicationBuilder</c>) installs middleware that writes full exception detail and stack traces
/// back to the client; that belongs in Development only, so the rule reports the invocation when no
/// enclosing <c>if</c> statement or conditional expression whose condition calls a method named
/// <c>IsDevelopment</c> (for example <c>app.Environment.IsDevelopment()</c>) guards it. The guard scan
/// is a purely local ancestor walk with no data-flow. The extensions type is probed once per
/// compilation; a project without ASP.NET Core hosting registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1506UnguardedDeveloperExceptionPageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the middleware-registration method that is guarded.</summary>
    private const string UseDeveloperExceptionPageMethodName = "UseDeveloperExceptionPage";

    /// <summary>The metadata name of the type that declares the guarded extension method.</summary>
    private const string DeveloperExceptionPageExtensionsMetadataName =
        "Microsoft.AspNetCore.Builder.DeveloperExceptionPageExtensions";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.UnguardedDeveloperExceptionPage);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(DeveloperExceptionPageExtensionsMetadataName) is not { } extensionsType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, extensionsType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1506 for an unguarded <c>UseDeveloperExceptionPage</c> call on the gated extensions type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="extensionsType">The gated extensions type resolved for the compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, INamedTypeSymbol extensionsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a call to a member named 'UseDeveloperExceptionPage'.
        if (InvokedName.Of(invocation.Expression) is not UseDeveloperExceptionPageMethodName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: UseDeveloperExceptionPageMethodName } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, extensionsType)
            || DevelopmentGuard.Encloses(invocation))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UnguardedDeveloperExceptionPage,
            invocation.SyntaxTree,
            invocation.Span));
    }
}
