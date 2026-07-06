// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped modernization analyzer that steers older <c>as</c>/<c>is</c> idioms toward C# type patterns.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST2005 — an <c>as</c> cast compared to <c>null</c> (<c>x as T != null</c>) should be a type pattern.</description></item>
/// <item><description>SST2006 — a negated type test (<c>!(x is T)</c>) should use <c>is not</c>.</description></item>
/// <item><description>SST2007 — an <c>is</c> check followed by a cast local should use a declaration pattern.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PatternMatchingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernizationRules.UseIsPatternOverAsNullCheck,
        ModernizationRules.UseNegatedIsPattern,
        ModernizationRules.UseDeclarationPatternOverIsCheckAndCast);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeAsNullComparison, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeNegatedIs, SyntaxKind.LogicalNotExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIsCheckFollowedByCast, SyntaxKind.IfStatement);
    }

    /// <summary>Returns the <c>as</c> expression on either side of a null comparison, or <see langword="null"/>.</summary>
    /// <param name="comparison">The equality comparison.</param>
    /// <returns>The <c>as</c> expression compared to null, or <see langword="null"/> when the shape does not match.</returns>
    internal static BinaryExpressionSyntax? GetAsOperandComparedToNull(BinaryExpressionSyntax comparison)
    {
        if (IsNullLiteral(comparison.Right))
        {
            return AsCastOperand(comparison.Left);
        }

        if (!IsNullLiteral(comparison.Left))
        {
            return null;
        }

        return AsCastOperand(comparison.Right);
    }

    /// <summary>Unwraps any enclosing parentheses to reach the inner expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    internal static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Builds the <c>operand is not type</c> pattern expression with single-space spacing.</summary>
    /// <param name="operand">The value being tested.</param>
    /// <param name="type">The type being tested for.</param>
    /// <returns>An <c>is not</c> pattern expression.</returns>
    internal static IsPatternExpressionSyntax BuildIsNotPattern(ExpressionSyntax operand, TypeSyntax type)
    {
        var typePattern = SyntaxFactory.TypePattern(type.WithoutTrivia());
        var notPattern = SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space), typePattern);
        return SyntaxFactory.IsPatternExpression(
            operand.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.IsKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            notPattern);
    }

    /// <summary>Builds the <c>operand is type</c> expression with single-space spacing.</summary>
    /// <param name="operand">The value being tested.</param>
    /// <param name="type">The type being tested for.</param>
    /// <returns>An <c>is</c> type-test expression.</returns>
    internal static BinaryExpressionSyntax BuildIsTypeTest(ExpressionSyntax operand, TypeSyntax type)
    {
        var isOperator = SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.IsKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        return SyntaxFactory.BinaryExpression(SyntaxKind.IsExpression, operand.WithoutTrivia(), isOperator, type.WithoutTrivia());
    }

    /// <summary>Returns the operand as an <c>as</c> expression once parentheses are peeled, or <see langword="null"/>.</summary>
    /// <param name="operand">The comparison operand.</param>
    /// <returns>The <c>as</c> expression, or <see langword="null"/> when it is not one.</returns>
    private static BinaryExpressionSyntax? AsCastOperand(ExpressionSyntax operand)
        => Unwrap(operand) is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpression ? asExpression : null;

    /// <summary>Reports SST2005 when an <c>as</c> cast is compared to <c>null</c> with a reference type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeAsNullComparison(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (GetAsOperandComparedToNull(comparison) is not { } asExpression)
        {
            return;
        }

        // 'x is T' is only equivalent for reference types; 'x as int?' would need a different pattern.
        var targetType = context.SemanticModel.GetTypeInfo(asExpression.Right, context.CancellationToken).Type;
        if (targetType?.IsReferenceType != true)
        {
            return;
        }

        // The '== null' branch is fixed to 'x is not T' (needs C# 9); the '!= null' branch becomes a plain 'x is T' (C# 1).
        var isEqualNull = comparison.IsKind(SyntaxKind.EqualsExpression);
        if (isEqualNull && context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp9 })
        {
            return;
        }

        var suggestion = isEqualNull ? "is not" : "is";
        context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.UseIsPatternOverAsNullCheck, comparison.GetLocation(), suggestion));
    }

    /// <summary>Reports SST2006 when a <c>!</c> wraps a type test (<c>!(x is T)</c>).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeNegatedIs(SyntaxNodeAnalysisContext context)
    {
        // The fix emits an 'is not' pattern, which requires C# 9.
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp9 })
        {
            return;
        }

        var not = (PrefixUnaryExpressionSyntax)context.Node;

        // Only the type-test form ('x is T') negates cleanly; declaration patterns ('x is T t') bind a name.
        if (Unwrap(not.Operand) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpression
            || isExpression.Right is not TypeSyntax)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.UseNegatedIsPattern, not.GetLocation()));
    }

    /// <summary>Reports SST2007 when an <c>is</c> check is immediately followed by a local initialized from the matching cast.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIsCheckFollowedByCast(SyntaxNodeAnalysisContext context)
    {
        // The fix emits a declaration pattern ('x is T t'), which requires C# 7.
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7 })
        {
            return;
        }

        if (!TryGetIsCheckCastCandidate((IfStatementSyntax)context.Node, out var candidate))
        {
            return;
        }

        if (!IsSameStableOperand(candidate.IsExpression.Left, candidate.Cast.Expression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        if (!IsSameType(candidate.IsType, candidate.Cast.Type, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ModernizationRules.UseDeclarationPatternOverIsCheckAndCast,
            candidate.IfStatement.Condition.GetLocation(),
            candidate.Variable.Identifier.ValueText));
    }

    /// <summary>Gets the syntactic parts of an <c>is</c> check followed by a cast local.</summary>
    /// <param name="ifStatement">The if statement to inspect.</param>
    /// <param name="candidate">The matched candidate parts.</param>
    /// <returns><see langword="true"/> when the syntax shape matches.</returns>
    private static bool TryGetIsCheckCastCandidate(IfStatementSyntax ifStatement, out IsCheckCastCandidate candidate)
    {
        candidate = default;
        if (ifStatement.Statement is not BlockSyntax { Statements.Count: > 0 } block
            || Unwrap(ifStatement.Condition) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpression
            || isExpression.Right is not TypeSyntax isType
            || block.Statements[0] is not LocalDeclarationStatementSyntax declaration
            || declaration.Declaration.Variables.Count != 1
            || declaration.Declaration.Variables[0] is not { Initializer.Value: CastExpressionSyntax cast } variable)
        {
            return false;
        }

        candidate = new IsCheckCastCandidate(ifStatement, isExpression, isType, cast, variable);
        return true;
    }

    /// <summary>Returns whether two expressions read the same stable symbol.</summary>
    /// <param name="left">The first expression.</param>
    /// <param name="right">The second expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when both expressions bind to the same local or parameter.</returns>
    private static bool IsSameStableOperand(ExpressionSyntax left, ExpressionSyntax right, SemanticModel model, CancellationToken cancellationToken)
    {
        var candidateSymbol = model.GetSymbolInfo(left, cancellationToken).Symbol;
        return IsStablePatternOperand(candidateSymbol)
            && SymbolEqualityComparer.Default.Equals(candidateSymbol, model.GetSymbolInfo(right, cancellationToken).Symbol);
    }

    /// <summary>Returns whether two type syntaxes bind to the same type.</summary>
    /// <param name="left">The first type syntax.</param>
    /// <param name="right">The second type syntax.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when both type syntaxes bind to the same type.</returns>
    private static bool IsSameType(TypeSyntax left, TypeSyntax right, SemanticModel model, CancellationToken cancellationToken)
    {
        var checkedType = model.GetTypeInfo(left, cancellationToken).Type;
        return checkedType is not null
            && SymbolEqualityComparer.Default.Equals(checkedType, model.GetTypeInfo(right, cancellationToken).Type);
    }

    /// <summary>Returns whether the operand can be read again without invoking user code.</summary>
    /// <param name="symbol">The symbol read by the type check and cast.</param>
    /// <returns><see langword="true"/> for locals and parameters.</returns>
    private static bool IsStablePatternOperand(ISymbol? symbol)
        => symbol is ILocalSymbol or IParameterSymbol;

    /// <summary>Returns whether an expression is the <c>null</c> literal.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> for a <c>null</c> literal.</returns>
    private static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Matched parts for an <c>is</c> check followed by a cast local.</summary>
    /// <param name="IfStatement">The containing if statement.</param>
    /// <param name="IsExpression">The type-test expression.</param>
    /// <param name="IsType">The type from the type-test expression.</param>
    /// <param name="Cast">The local initializer cast expression.</param>
    /// <param name="Variable">The declared local variable.</param>
    private readonly record struct IsCheckCastCandidate(
        IfStatementSyntax IfStatement,
        BinaryExpressionSyntax IsExpression,
        TypeSyntax IsType,
        CastExpressionSyntax Cast,
        VariableDeclaratorSyntax Variable);
}
