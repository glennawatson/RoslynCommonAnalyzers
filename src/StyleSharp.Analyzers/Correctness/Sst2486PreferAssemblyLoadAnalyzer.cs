// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// before anything is reported. <see cref="System.Reflection.Assembly"/> is resolved once per compilation and the whole
/// rule is gated on it, so a compilation that somehow lacks the type pays nothing.
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.PreferAssemblyLoad);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the invocation walk only when <see cref="System.Reflection.Assembly"/> resolves.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName("System.Reflection.Assembly") is not { } assemblyType)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, assemblyType), SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one path or partial-name assembly-load call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assemblyType">The resolved <see cref="System.Reflection.Assembly"/> symbol.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol assemblyType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var memberName } memberAccess
            || GetReason(memberName) is not { } reason)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
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
}
