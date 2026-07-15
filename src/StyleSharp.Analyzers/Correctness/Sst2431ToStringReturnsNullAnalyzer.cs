// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an overridden <c>ToString</c> that can return <see langword="null"/> (SST2431): a null literal,
/// <c>null!</c>, a parenthesised or cast null, or a null branch of a conditional, in either the expression
/// body or a <c>return</c> statement.
/// </summary>
/// <remarks>
/// The clean path is three token checks — the method is named <c>ToString</c>, is <c>override</c>, and takes
/// no parameters — so every other method is rejected before anything is allocated. Detection is purely
/// syntactic: a null a caller would receive is spelled out at the return site, and the walk stops at nested
/// lambdas and local functions, whose <c>return</c> does not return from <c>ToString</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2431ToStringReturnsNullAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name an override must carry to be <c>object.ToString</c>.</summary>
    private const string ToStringName = "ToString";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.ToStringReturnsNull);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Analyzes one method declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Identifier.ValueText != ToStringName
            || method.ParameterList.Parameters.Count != 0
            || !method.Modifiers.Any(SyntaxKind.OverrideKeyword))
        {
            return;
        }

        var results = new List<ExpressionSyntax>();
        if (method.ExpressionBody is { } arrow)
        {
            CollectNullReturns(arrow.Expression, results);
        }
        else if (method.Body is { } body)
        {
            CollectReturnStatements(body, results);
        }

        for (var i = 0; i < results.Count; i++)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.ToStringReturnsNull, results[i].GetLocation()));
        }
    }

    /// <summary>Walks a statement subtree for the method's own <c>return</c> expressions, not those of nested functions.</summary>
    /// <param name="node">The statement subtree to walk.</param>
    /// <param name="results">The list receiving every reportable null.</param>
    private static void CollectReturnStatements(SyntaxNode node, List<ExpressionSyntax> results)
    {
        foreach (var child in node.ChildNodes())
        {
            // A return inside a nested lambda or local function returns from that function, not from ToString.
            if (child is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                continue;
            }

            if (child is ReturnStatementSyntax { Expression: { } expression })
            {
                CollectNullReturns(expression, results);
                continue;
            }

            CollectReturnStatements(child, results);
        }
    }

    /// <summary>Adds every reportable null a return expression can hand back, descending only into conditional branches and wrappers.</summary>
    /// <param name="expression">The return expression to classify.</param>
    /// <param name="results">The list receiving every reportable null.</param>
    private static void CollectNullReturns(ExpressionSyntax expression, List<ExpressionSyntax> results)
    {
        if (IsAlwaysNull(expression))
        {
            results.Add(expression);
            return;
        }

        if (expression is ConditionalExpressionSyntax conditional)
        {
            CollectNullReturns(conditional.WhenTrue, results);
            CollectNullReturns(conditional.WhenFalse, results);
            return;
        }

        if (Unwrap(expression) is not { } inner)
        {
            return;
        }

        CollectNullReturns(inner, results);
    }

    /// <summary>Returns whether an expression unconditionally evaluates to null.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> for null, <c>null!</c>, and a parenthesised or cast null.</returns>
    private static bool IsAlwaysNull(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression }
            || (Unwrap(expression) is { } inner && IsAlwaysNull(inner));

    /// <summary>Removes a null-preserving wrapper — parentheses, a cast, or a <c>null!</c> suppression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The wrapped expression, or <see langword="null"/> when there is no wrapper.</returns>
    private static ExpressionSyntax? Unwrap(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
        CastExpressionSyntax cast => cast.Expression,
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => suppressed.Operand,
        _ => null,
    };
}
