// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>Assembly.LoadFrom(...)</c>, <c>Assembly.LoadFile(...)</c>, and <c>Assembly.LoadWithPartialName(...)</c>
/// and steers the call to <c>Assembly.Load</c> with a full display name (SST2486). Each of the three loads an
/// assembly outside the default name-based binding context, which is a recurring source of duplicate-identity
/// type-mismatch bugs: <c>LoadFrom</c> resolves through a separate context whose types do not equal the ones the
/// default context loads, <c>LoadFile</c> loads with no context and never unifies with an already-loaded copy, and
/// <c>LoadWithPartialName</c> resolves a partial name nondeterministically and is deprecated.
/// </summary>
/// <remarks>
/// The clean path is a syntax check: only a member-access invocation whose member name is one of the three reaches
/// the semantic model, which then confirms the bound method's containing type is <see cref="System.Reflection.Assembly"/>
/// before anything is reported. <see cref="System.Reflection.Assembly"/> is resolved on the first syntactic candidate
/// and cached for the compilation, including when the type is absent.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2486PreferAssemblyLoadAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The load-by-path API whose returned types live in a separate binding context.</summary>
    private const string LoadFromName = "LoadFrom";

    /// <summary>The load-by-path API that loads with no binding context.</summary>
    private const string LoadFileName = "LoadFile";

    /// <summary>The deprecated load-by-partial-name API.</summary>
    private const string LoadWithPartialNameName = "LoadWithPartialName";

    /// <summary>The reason phrase for <see cref="LoadFromName"/>, tailored into the message.</summary>
    private const string LoadFromReason =
        "resolves through a separate binding context, so a type it returns can fail to equal the same type loaded normally";

    /// <summary>The reason phrase for <see cref="LoadFileName"/>, tailored into the message.</summary>
    private const string LoadFileReason =
        "loads with no binding context, so it never unifies with an already-loaded copy of the assembly and creates a duplicate identity";

    /// <summary>The reason phrase for <see cref="LoadWithPartialNameName"/>, tailored into the message.</summary>
    private const string LoadWithPartialNameReason =
        "resolves a partial name to whichever assembly the runtime happens to find, and is deprecated";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.PreferAssemblyLoad);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the invocation walk with a deferred assembly-type lookup.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var assemblyTypes = new AssemblyTypes(context.Compilation);
        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, assemblyTypes), SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one path or partial-name assembly-load call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assemblyTypes">The deferred assembly-type lookup for this compilation.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, AssemblyTypes assemblyTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var memberName } memberAccess
            || GetReason(memberName) is not { } reason)
        {
            return;
        }

        if (assemblyTypes.Get() is not { } assemblyType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, assemblyType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.PreferAssemblyLoad,
            memberAccess.Name.GetLocation(),
            memberName,
            reason));
    }

    /// <summary>Returns the tailored reason phrase for a load API name, or <see langword="null"/> for anything else.</summary>
    /// <param name="memberName">The invoked member's identifier text.</param>
    /// <returns>The reason phrase when the member is a reported load API; otherwise <see langword="null"/>.</returns>
    private static string? GetReason(string memberName) => memberName switch
    {
        LoadFromName => LoadFromReason,
        LoadFileName => LoadFileReason,
        LoadWithPartialNameName => LoadWithPartialNameReason,
        _ => null,
    };

    /// <summary>Resolves the assembly type only when a candidate call needs it.</summary>
    /// <param name="compilation">The compilation whose assembly type is cached.</param>
    private sealed class AssemblyTypes(Compilation compilation)
    {
        /// <summary>The published lookup result, including a missing type.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the cached assembly type, resolving it on first use.</summary>
        /// <returns>The assembly type, or <see langword="null"/> when absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName("System.Reflection.Assembly")])[0];
    }
}
