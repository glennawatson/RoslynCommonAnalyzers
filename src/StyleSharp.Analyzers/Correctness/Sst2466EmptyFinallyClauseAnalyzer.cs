// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>finally</c> clause with no statements (SST2466). It runs no cleanup, so the <c>try</c> it is
/// attached to guarantees nothing while still reading as though unwinding is handled.
/// </summary>
/// <remarks>
/// A clause holding only comments is left alone: the comment is often the note explaining why nothing runs.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2466EmptyFinallyClauseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.EmptyFinallyClause);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FinallyClause);
    }

    /// <summary>Reports one empty <c>finally</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var clause = (FinallyClauseSyntax)context.Node;
        if (clause.Block.Statements.Count != 0)
        {
            return;
        }

        // An empty block holds its trivia on the braces themselves, so those two lists are the whole search.
        if (HasComment(clause.Block.OpenBraceToken.TrailingTrivia) || HasComment(clause.Block.CloseBraceToken.LeadingTrivia))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.EmptyFinallyClause,
            clause.SyntaxTree,
            clause.FinallyKeyword.Span));
    }

    /// <summary>Returns whether a trivia list holds a comment of any form.</summary>
    /// <param name="trivia">The trivia list to scan.</param>
    /// <returns><see langword="true"/> when a single-line, multi-line, or documentation comment is present.</returns>
    private static bool HasComment(SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
        {
            if (item.Kind() is SyntaxKind.SingleLineCommentTrivia
                or SyntaxKind.MultiLineCommentTrivia
                or SyntaxKind.SingleLineDocumentationCommentTrivia
                or SyntaxKind.MultiLineDocumentationCommentTrivia)
            {
                return true;
            }
        }

        return false;
    }
}
