// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports two or more consecutive statements that each call a member on the same receiver and get that
/// receiver's own type back (SST2265): <c>builder.Append(a); builder.Append(b);</c> is one fluent chain
/// written as separate statements. The rule is opt-in and off by default because whether a chain reads better
/// than separate statements is a house-style preference.
/// </summary>
/// <remarks>
/// The receiver must be a side-effect-free reference — an identifier, <c>this</c>, or a chain of those — so
/// evaluating it once in the folded chain matches evaluating it once per statement, and every call in the run
/// must return the receiver's own type so the chain binds. The semantic model is consulted only for a statement
/// that already matches the <c>receiver.Method(args);</c> shape.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2265FoldFluentCallChainAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest consecutive fluent statements that make a foldable chain.</summary>
    internal const int MinimumRunLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.FoldFluentCallChain);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Block);
    }

    /// <summary>Returns whether an expression is a side-effect-free receiver safe to evaluate once.</summary>
    /// <param name="expression">The receiver expression.</param>
    /// <returns><see langword="true"/> for an identifier, <c>this</c>, or a member-access chain of those.</returns>
    internal static bool IsPureReceiver(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax member => member.IsKind(SyntaxKind.SimpleMemberAccessExpression) && IsPureReceiver(member.Expression),
        _ => false,
    };

    /// <summary>Returns the receiver of a statement that is a fluent call on a pure receiver.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="statement">The statement to inspect.</param>
    /// <returns>The receiver expression, or <see langword="null"/> when the statement is not such a call.</returns>
    internal static ExpressionSyntax? GetFluentReceiver(SemanticModel model, StatementSyntax statement)
    {
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || !IsPureReceiver(memberAccess.Expression))
        {
            return null;
        }

        var invocationType = model.GetTypeInfo(invocation).Type;
        var receiverType = model.GetTypeInfo(memberAccess.Expression).Type;
        return invocationType is not null && receiverType is not null && SymbolEqualityComparer.Default.Equals(invocationType, receiverType)
            ? memberAccess.Expression
            : null;
    }

    /// <summary>Counts the fluent-call statements starting at an index that share the first one's receiver.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="block">The block whose statements are scanned.</param>
    /// <param name="startIndex">The index of the first statement in the candidate run.</param>
    /// <returns>The number of consecutive statements in the run, which is at least one for a fluent call.</returns>
    internal static int CountFluentRun(SemanticModel model, BlockSyntax block, int startIndex)
    {
        if (GetFluentReceiver(model, block.Statements[startIndex]) is not { } receiver)
        {
            return 0;
        }

        var count = 1;
        for (var i = startIndex + 1; i < block.Statements.Count; i++)
        {
            if (GetFluentReceiver(model, block.Statements[i]) is not { } next || !next.IsEquivalentTo(receiver))
            {
                break;
            }

            count++;
        }

        return count;
    }

    /// <summary>Reports each maximal run of consecutive fluent calls on one receiver in a block.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var block = (BlockSyntax)context.Node;
        var index = 0;
        while (index < block.Statements.Count)
        {
            var count = CountFluentRun(context.SemanticModel, block, index);
            if (count < MinimumRunLength)
            {
                index += count == 0 ? 1 : count;
                continue;
            }

            var receiver = ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)((ExpressionStatementSyntax)block.Statements[index]).Expression).Expression).Expression;
            context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.FoldFluentCallChain, receiver.GetLocation(), receiver.ToString()));
            index += count;
        }
    }
}
