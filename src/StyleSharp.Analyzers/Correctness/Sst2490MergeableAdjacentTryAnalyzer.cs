// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>try</c> statement that immediately follows another <c>try</c> in the same block when the
/// two carry identical catch/finally handling (SST2490): the same catch clauses — count, caught type,
/// exception filter, and body — and the same finally clause. The pair can collapse into one <c>try</c>
/// wrapping both bodies, so the shared handling is written once.
/// </summary>
/// <remarks>
/// The rule registers on <see cref="SyntaxKind.TryStatement"/> and anchors on the first <c>try</c> of a
/// pair, looking one statement forward. The clean path — a <c>try</c> whose next sibling is not a
/// <c>try</c> — costs a parent-kind test and one index compare and allocates nothing; the trivia-stripped
/// <see cref="SyntaxFactory.AreEquivalent(SyntaxNode, SyntaxNode, bool)"/> handler comparison runs only for
/// the rare adjacent-<c>try</c> pair. A handler that guards nothing (an empty <c>finally</c>, or a bare
/// <c>catch</c> with no caught type, filter, or body) is not reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2490MergeableAdjacentTryAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.MergeableAdjacentTry);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TryStatement);
    }

    /// <summary>Reports the second <c>try</c> of an adjacent pair that share identical handling.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var first = (TryStatementSyntax)context.Node;
        if (first.Parent is not BlockSyntax block)
        {
            return;
        }

        var statements = block.Statements;
        var lastAnchor = statements.Count - 1;
        var start = first.SpanStart;
        for (var i = 0; i < lastAnchor; i++)
        {
            if (statements[i].SpanStart != start)
            {
                continue;
            }

            if (statements[i + 1] is TryStatementSyntax second
                && HasSubstantiveHandler(first)
                && HandlersAreEquivalent(first, second))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.MergeableAdjacentTry,
                    second.TryKeyword.GetLocation()));
            }

            return;
        }
    }

    /// <summary>Determines whether a <c>try</c>'s handler does real work, so merging it is worthwhile.</summary>
    /// <param name="tryStatement">The try statement whose handler is examined.</param>
    /// <returns><see langword="true"/> when a catch clause caught a type, ran a filter, or carried a body, or a finally block had statements.</returns>
    private static bool HasSubstantiveHandler(TryStatementSyntax tryStatement)
    {
        var catches = tryStatement.Catches;
        for (var i = 0; i < catches.Count; i++)
        {
            var clause = catches[i];
            if (clause.Declaration is not null
                || clause.Filter is not null
                || clause.Block.Statements.Count > 0)
            {
                return true;
            }
        }

        return tryStatement.Finally?.Block.Statements.Count > 0;
    }

    /// <summary>Determines whether two <c>try</c> statements have syntactically identical catch/finally handling.</summary>
    /// <param name="first">The first try statement.</param>
    /// <param name="second">The second try statement.</param>
    /// <returns><see langword="true"/> when the catch clauses and finally clause are equivalent ignoring trivia.</returns>
    private static bool HandlersAreEquivalent(TryStatementSyntax first, TryStatementSyntax second)
    {
        var firstCatches = first.Catches;
        var secondCatches = second.Catches;
        if (firstCatches.Count != secondCatches.Count)
        {
            return false;
        }

        for (var i = 0; i < firstCatches.Count; i++)
        {
            if (!SyntaxFactory.AreEquivalent(firstCatches[i], secondCatches[i], topLevel: false))
            {
                return false;
            }
        }

        var firstFinally = first.Finally;
        var secondFinally = second.Finally;
        if (firstFinally is null)
        {
            return secondFinally is null;
        }

        return secondFinally is not null
            && SyntaxFactory.AreEquivalent(firstFinally, secondFinally, topLevel: false);
    }
}
