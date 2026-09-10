// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an assembly-internal abstract class or record whose direct descendants are all declared in
/// the compilation, where the C# 15 <c>closed</c> modifier would make a switch over it exhaustive (SST2337).
/// </summary>
/// <remarks>
/// Completeness is a property of the whole compilation rather than of one declaration, so the counting
/// happens in symbol actions and the reporting at compilation end. See <see cref="ClosedHierarchyTally"/>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2337ClosedHierarchyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(DesignRules.PreferClosedHierarchy);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var tally = new ClosedHierarchyTally();
            start.RegisterSymbolAction(symbolContext => tally.Observe((INamedTypeSymbol)symbolContext.Symbol), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(tally.Report);
        });
    }
}
