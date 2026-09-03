// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>An analyzer that reports nothing, used to measure what the harness costs on its own.</summary>
/// <remarks>
/// Running any analyzer through <c>GetAnalyzerDiagnosticsAsync</c> makes the compiler bind the trees it
/// walks, and traces show those frames dominate what these benchmarks allocate. Measuring the same corpus
/// with an analyzer that reports nothing gives the floor to subtract, so a rule's own cost is the
/// difference rather than a number the compiler appears to own.
/// <para>
/// It asks for a semantic model per tree and does nothing with it, which is the work any semantic rule
/// causes before its own logic runs. Binding through a plain compile instead would put the floor well
/// below what a real rule can reach — that measures 5 MB where the analyzer run measures 96 — and credit
/// the rule with the driver's cost.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HarnessBaselineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>A descriptor that keeps the analyzer active, and is never reported.</summary>
    private static readonly DiagnosticDescriptor NeverReported = new(
        "BENCH0000",
        "Harness baseline",
        "Harness baseline",
        "Benchmark",
        DiagnosticSeverity.Hidden,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [NeverReported];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSemanticModelAction(static _ => { });
    }
}
