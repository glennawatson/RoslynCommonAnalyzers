// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>if</c> whose entire body is another <c>if</c>, with no <c>else</c> on either (SST2013). The
/// two conditions are one condition written across two levels of indentation, and <c>&amp;&amp;</c> says it in
/// one.
/// </summary>
/// <remarks>
/// Both <c>else</c> clauses have to be absent for the merge to preserve meaning: an <c>else</c> on the outer
/// <c>if</c> runs when the first condition fails, an <c>else</c> on the inner one when the second fails, and
/// a merged condition can no longer tell those apart. The inner <c>if</c> may be written bare or as the only
/// statement of a block; nothing else may share the block, or the merge would move that statement under a
/// condition it was never guarded by.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2013MergeNestedIfAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.MergeNestedIf);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    /// <summary>Returns the sole nested <c>if</c> an outer <c>if</c> wraps, when the pair can be merged.</summary>
    /// <param name="outer">The outer if statement.</param>
    /// <returns>The inner if statement, or <see langword="null"/> when the shape does not match.</returns>
    internal static IfStatementSyntax? GetMergeableInnerIf(IfStatementSyntax outer)
    {
        if (outer.Else is not null)
        {
            return null;
        }

        var inner = outer.Statement switch
        {
            IfStatementSyntax bare => bare,
            BlockSyntax { Statements: { Count: 1 } statements } => statements[0] as IfStatementSyntax,
            _ => null,
        };

        return inner is { Else: null } ? inner : null;
    }

    /// <summary>Reports an outer <c>if</c> that does nothing but guard another one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var outer = (IfStatementSyntax)context.Node;
        if (GetMergeableInnerIf(outer) is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.MergeNestedIf, outer.IfKeyword.GetLocation()));
    }
}
