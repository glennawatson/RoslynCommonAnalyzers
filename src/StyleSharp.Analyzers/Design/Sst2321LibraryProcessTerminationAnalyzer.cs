// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a call to <c>Environment.Exit</c> or <c>Environment.FailFast</c> made from a class library
/// (SST2321). Either one ends the entire host process, so a library that makes the decision on its host's
/// behalf can tear down an unrelated web request, a test run, or a tool that only wanted to call one method.
/// </summary>
/// <remarks>
/// The rule is gated on the compilation being a library: in an executable — which does own its process —
/// nothing is registered, so the analyzer costs an application build nothing. It resolves
/// <c>System.Environment</c> once at compilation start and registers no callback when the type is absent,
/// and the per-invocation path is a member-name comparison that binds only the handful of calls that could
/// be one of the two members before confirming the containing type really is <c>System.Environment</c>.
/// <para>
/// When <c>Microsoft.Extensions.Hosting.IHostApplicationLifetime</c> resolves in the compilation, the
/// message points at it as the hosted-application way to request an orderly shutdown; otherwise the message
/// stays generic, so it never names an API the analyzed project cannot use.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2321LibraryProcessTerminationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the type that owns both terminating members.</summary>
    private const string EnvironmentMetadataName = "System.Environment";

    /// <summary>The metadata name of the hosted-application lifetime the message can point at.</summary>
    private const string HostApplicationLifetimeMetadataName = "Microsoft.Extensions.Hosting.IHostApplicationLifetime";

    /// <summary>The suffix appended to the message when the hosted-application lifetime is available.</summary>
    private const string HostLifetimeHint = " (in a hosted application, request an orderly shutdown through IHostApplicationLifetime instead)";

    /// <summary>The name of the member that exits the process with a code.</summary>
    private const string ExitMemberName = "Exit";

    /// <summary>The name of the member that fails the process fast.</summary>
    private const string FailFastMemberName = "FailFast";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.LibraryProcessTermination);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the rule only for a library whose compilation can bind the terminating type.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.Options.OutputKind != OutputKind.DynamicallyLinkedLibrary)
        {
            return;
        }

        var environmentType = context.Compilation.GetTypeByMetadataName(EnvironmentMetadataName);
        if (environmentType is null)
        {
            return;
        }

        var hostLifetimeAvailable = context.Compilation.GetTypeByMetadataName(HostApplicationLifetimeMetadataName) is not null;
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, environmentType, hostLifetimeAvailable),
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports a process-terminating call made from library code.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="environmentType">The resolved <c>System.Environment</c> symbol.</param>
    /// <param name="hostLifetimeAvailable">Whether the hosted-application lifetime resolves in the compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol environmentType, bool hostLifetimeAvailable)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: ExitMemberName or FailFastMemberName })
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, environmentType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.LibraryProcessTermination,
            invocation.GetLocation(),
            $"{method.ContainingType.Name}.{method.Name}",
            hostLifetimeAvailable ? HostLifetimeHint : string.Empty));
    }
}
