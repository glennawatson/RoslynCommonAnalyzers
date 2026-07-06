// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a catch clause whose body is exactly a bare <c>throw;</c> when it declares no filter and
/// is the last clause of its try statement (SST1470). Such a clause re-raises the exception
/// unchanged, so removing it cannot alter how the exception propagates. Earlier rethrow-only
/// clauses are left alone: removing a non-last clause would let a later, more general clause catch
/// the exception, which is a behavior change. Purely syntactic — no semantic model is used.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1470RemoveRethrowOnlyCatchAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RemoveRethrowOnlyCatch);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    /// <summary>Returns whether a catch clause has no filter and a body that is exactly a bare <c>throw;</c>.</summary>
    /// <param name="catchClause">The catch clause.</param>
    /// <returns><see langword="true"/> for a filterless, rethrow-only clause.</returns>
    internal static bool IsRethrowOnly(CatchClauseSyntax catchClause)
        => catchClause.Filter is null
            && catchClause.Block.Statements.Count == 1
            && catchClause.Block.Statements[0] is ThrowStatementSyntax { Expression: null };

    /// <summary>Reports the last catch clause of a try statement when it only rethrows.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (!IsRethrowOnly(catchClause) || !IsLastCatchClause(catchClause))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.RemoveRethrowOnlyCatch, catchClause.CatchKeyword.GetLocation()));
    }

    /// <summary>Returns whether a catch clause is the last clause of its try statement.</summary>
    /// <param name="catchClause">The catch clause.</param>
    /// <returns><see langword="true"/> when no later catch clause exists.</returns>
    private static bool IsLastCatchClause(CatchClauseSyntax catchClause)
    {
        if (catchClause.Parent is not TryStatementSyntax tryStatement)
        {
            return false;
        }

        var catches = tryStatement.Catches;
        return catches[catches.Count - 1] == catchClause;
    }
}
