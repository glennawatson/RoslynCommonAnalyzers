// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a catch block that hand-rolls an exception filter: an opening <c>if</c> whose losing
/// branch is a bare <c>throw;</c> (SST2009). The condition can move into a <c>catch ... when</c>
/// clause instead, which skips the handler without unwinding the stack. Only side-effect-free
/// conditions are reported, because an exception thrown inside a <c>when</c> filter is silently
/// treated as <see langword="false"/> rather than propagated, so moving a throwing condition
/// could change behavior. The rule is gated on C# 6, where exception filters arrived.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2009UseExceptionFilterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric value of <c>LanguageVersion.CSharp6</c>, the first version with exception filters.</summary>
    private const int CSharp6 = 6;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.UseExceptionFilter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    /// <summary>Returns whether a catch block's opening <c>if</c> matches a hand-rolled filter shape.</summary>
    /// <param name="ifStatement">The catch block's first statement.</param>
    /// <param name="statementCount">The catch block's total statement count.</param>
    /// <returns><see langword="true"/> when the condition can move into a <c>when</c> clause.</returns>
    internal static bool MatchesFilterShape(IfStatementSyntax ifStatement, int statementCount)
    {
        if (ifStatement.Else is { } elseClause)
        {
            return statementCount == 1 && IsBareRethrow(ifStatement.Statement) != IsBareRethrow(elseClause.Statement);
        }

        return statementCount > 1 && IsBareRethrow(ifStatement.Statement);
    }

    /// <summary>Returns whether a statement is a bare <c>throw;</c>, directly or as a block's only statement.</summary>
    /// <param name="statement">The branch statement.</param>
    /// <returns><see langword="true"/> for a bare rethrow.</returns>
    internal static bool IsBareRethrow(StatementSyntax statement)
        => statement switch
        {
            ThrowStatementSyntax { Expression: null } => true,
            BlockSyntax { Statements: { Count: 1 } statements } => statements[0] is ThrowStatementSyntax { Expression: null },
            _ => false
        };

    /// <summary>
    /// Returns whether a condition is syntactically side-effect-free and so can move into a
    /// <c>when</c> filter without changing behavior. Allows identifiers, member-access chains,
    /// literals, <c>is</c> type/pattern tests of allowed operands, parentheses, prefix <c>!</c>,
    /// comparisons, and <c>&amp;&amp;</c>/<c>||</c> over allowed content; anything that can run
    /// arbitrary code (invocations, element access, await, assignments, object creation) is rejected.
    /// </summary>
    /// <param name="expression">The condition to inspect.</param>
    /// <returns><see langword="true"/> when the condition is safe to move.</returns>
    internal static bool IsSideEffectFreeCondition(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax or LiteralExpressionSyntax => true,
            MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } memberAccess => IsSideEffectFreeCondition(memberAccess.Expression),
            ParenthesizedExpressionSyntax parenthesized => IsSideEffectFreeCondition(parenthesized.Expression),
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } logicalNot => IsSideEffectFreeCondition(logicalNot.Operand),
            IsPatternExpressionSyntax isPattern => IsSideEffectFreeCondition(isPattern.Expression) && IsSideEffectFreePattern(isPattern.Pattern),
            BinaryExpressionSyntax binary => IsSideEffectFreeBinary(binary),
            _ => false,
        };

    /// <summary>Reports SST2009 at the <c>if</c> keyword of a catch block that hand-rolls a filter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (catchClause.Filter is not null)
        {
            return;
        }

        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { } options || (int)options.LanguageVersion < CSharp6)
        {
            return;
        }

        var statements = catchClause.Block.Statements;
        if (statements.Count == 0
            || statements[0] is not IfStatementSyntax ifStatement
            || !MatchesFilterShape(ifStatement, statements.Count)
            || !IsSideEffectFreeCondition(ifStatement.Condition))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.UseExceptionFilter, ifStatement.IfKeyword.GetLocation()));
    }

    /// <summary>Returns whether a binary expression is an allowed comparison, logical join, or <c>is</c> type test.</summary>
    /// <param name="binary">The binary expression.</param>
    /// <returns><see langword="true"/> when the operator and both operands are allowed.</returns>
    private static bool IsSideEffectFreeBinary(BinaryExpressionSyntax binary)
        => binary.Kind() switch
        {
            SyntaxKind.IsExpression => IsSideEffectFreeCondition(binary.Left),
            var kind when IsComparisonOrLogicalKind(kind) => IsSideEffectFreeCondition(binary.Left) && IsSideEffectFreeCondition(binary.Right),
            _ => false,
        };

    /// <summary>Returns whether a binary operator kind is an allowed comparison or short-circuit logical join.</summary>
    /// <param name="kind">The binary expression's syntax kind.</param>
    /// <returns><see langword="true"/> for equality, relational, <c>&amp;&amp;</c>, and <c>||</c> operators.</returns>
    private static bool IsComparisonOrLogicalKind(SyntaxKind kind)
        => kind is SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.LogicalAndExpression
            or SyntaxKind.LogicalOrExpression;

    /// <summary>Returns whether a pattern is a constant, type, or property pattern over allowed content.</summary>
    /// <param name="pattern">The pattern to inspect.</param>
    /// <returns><see langword="true"/> when the pattern cannot run arbitrary code and declares nothing.</returns>
    private static bool IsSideEffectFreePattern(PatternSyntax pattern)
        => pattern switch
        {
            ConstantPatternSyntax constant => IsSideEffectFreeCondition(constant.Expression),
            TypePatternSyntax => true,
            RecursivePatternSyntax { PositionalPatternClause: null, Designation: null, PropertyPatternClause: { } propertyClause } => AreSideEffectFreeSubpatterns(propertyClause),
            _ => false,
        };

    /// <summary>Returns whether every subpattern of a property pattern clause is allowed.</summary>
    /// <param name="propertyClause">The property pattern clause.</param>
    /// <returns><see langword="true"/> when every nested pattern is allowed.</returns>
    private static bool AreSideEffectFreeSubpatterns(PropertyPatternClauseSyntax propertyClause)
    {
        var subpatterns = propertyClause.Subpatterns;
        for (var i = 0; i < subpatterns.Count; i++)
        {
            if (!IsSideEffectFreePattern(subpatterns[i].Pattern))
            {
                return false;
            }
        }

        return true;
    }
}
