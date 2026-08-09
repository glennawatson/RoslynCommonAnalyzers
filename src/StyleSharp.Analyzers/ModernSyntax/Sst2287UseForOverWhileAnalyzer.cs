// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a while loop whose counter is declared immediately above it and stepped by its last statement
/// (SST2287): the three parts belong in a <c>for</c> header, which also scopes the counter to the loop.
/// </summary>
/// <remarks>
/// The match lives in <see cref="WhileLoopCounter"/> so the analyzer and its code fix agree on which loops
/// fold. Everything cheap is checked first — the body shape, the preceding declaration, the trailing step —
/// and the data-flow question of whether the counter outlives the loop is asked last, only for a loop that
/// already looks like a for.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2287UseForOverWhileAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(ModernSyntaxRules.UseForOverWhile);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.WhileStatement);
    }

    /// <summary>Reports one while loop that owns its counter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var loop = (WhileStatementSyntax)context.Node;
        if (!WhileLoopCounter.TryMatch(loop, context.SemanticModel, context.CancellationToken, out var parts))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UseForOverWhile,
            loop.SyntaxTree,
            TextSpan.FromBounds(loop.WhileKeyword.SpanStart, loop.CloseParenToken.Span.End),
            parts.CounterName));
    }
}
