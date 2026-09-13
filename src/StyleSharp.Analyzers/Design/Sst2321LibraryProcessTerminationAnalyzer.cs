// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a call to <c>Environment.Exit</c> or <c>Environment.FailFast</c> made from a class library
/// (SST2321). Either one ends the entire host process, so a library that makes the decision on its host's
/// behalf can tear down an unrelated web request, a test run, or a tool that only wanted to call one method.
/// </summary>
/// <remarks>
/// The per-invocation path first checks the member name and that the compilation is a library, then
/// resolves <c>System.Environment</c> and binds only calls that could be one of the two members.
/// Executables own their process and are never reported. The containing type must resolve and match
/// <c>System.Environment</c> before the rule reports a diagnostic.
/// <para>
/// When <c>Microsoft.Extensions.Hosting.IHostApplicationLifetime</c> resolves in the compilation, the
/// message points at it as the hosted-application way to request an orderly shutdown; otherwise the message
/// stays generic, so it never names an API the analyzed project cannot use.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2321LibraryProcessTerminationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The suffix appended to the message when the hosted-application lifetime is available.</summary>
    private const string HostLifetimeHint = " (in a hosted application, request an orderly shutdown through IHostApplicationLifetime instead)";

    /// <summary>The name of the member that exits the process with a code.</summary>
    private const string ExitMemberName = "Exit";

    /// <summary>The name of the member that fails the process fast.</summary>
    private const string FailFastMemberName = "FailFast";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.LibraryProcessTermination);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            var types = new ProcessTypes(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, types), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports a process-terminating call made from library code.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation-scoped process type cache.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, ProcessTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: ExitMemberName or FailFastMemberName })
        {
            return;
        }

        if (context.Compilation.Options.OutputKind != OutputKind.DynamicallyLinkedLibrary
            || types.GetEnvironment() is not { } environmentType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, environmentType))
        {
            return;
        }

        var hostLifetimeAvailable = types.GetHostLifetime() is not null;
        context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.LibraryProcessTermination,
            invocation.GetLocation(),
            $"{method.ContainingType.Name}.{method.Name}",
            hostLifetimeAvailable ? HostLifetimeHint : string.Empty));
    }

    /// <summary>Resolves each process-related type on first demand within a compilation.</summary>
    /// <param name="compilation">The compilation whose process-related types are resolved.</param>
    private sealed class ProcessTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the type that owns both terminating members.</summary>
        private const string EnvironmentMetadataName = "System.Environment";

        /// <summary>The metadata name of the hosted-application lifetime the message can point at.</summary>
        private const string HostApplicationLifetimeMetadataName = "Microsoft.Extensions.Hosting.IHostApplicationLifetime";

        /// <summary>The cached environment type, with a null element when the type is absent.</summary>
        private INamedTypeSymbol?[]? _environment;

        /// <summary>The cached host-lifetime type, with a null element when the type is absent.</summary>
        private INamedTypeSymbol?[]? _hostLifetime;

        /// <summary>Resolves the environment type on first demand and caches its absence too.</summary>
        /// <returns>The environment type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetEnvironment() => (_environment ??= [compilation.GetTypeByMetadataName(EnvironmentMetadataName)])[0];

        /// <summary>Resolves the host-lifetime type on first demand and caches its absence too.</summary>
        /// <returns>The host-lifetime type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetHostLifetime() => (_hostLifetime ??= [compilation.GetTypeByMetadataName(HostApplicationLifetimeMetadataName)])[0];
    }
}
