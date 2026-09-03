// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a null guard conjoined with a member read on the same value (SST2285):
/// <c>x != null &amp;&amp; x.Count &gt; 0</c> becomes <c>x?.Count &gt; 0</c>, and a <c>bool?</c> tested
/// through <c>b != null &amp;&amp; b.Value</c> becomes <c>b == true</c>.
/// </summary>
/// <remarks>
/// The decision lives in <see cref="NullCheckConditionalAccessFold"/> so the analyzer and its code fix agree
/// on which conjunctions fold and how.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2285FoldNullCheckIntoConditionalAccessAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ModernSyntaxRules.FoldNullCheckIntoConditionalAccess);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalAndExpression);
    }

    /// <summary>Reports one guarded member read that folds into a conditional access.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var conjunction = (BinaryExpressionSyntax)context.Node;
        var kind = NullCheckConditionalAccessFold.Classify(
            conjunction,
            context.SemanticModel,
            context.CancellationToken,
            out var receiver,
            out _);

        if (kind == NullCheckFoldKind.None)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.FoldNullCheckIntoConditionalAccess,
            conjunction.SyntaxTree,
            conjunction.Span,
            receiver.ToString()));
    }
}
