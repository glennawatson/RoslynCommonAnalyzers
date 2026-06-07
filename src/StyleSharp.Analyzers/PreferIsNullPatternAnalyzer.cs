// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports null checks written as <c>x == null</c> / <c>x != null</c> (SST1149), preferring
/// the pattern forms <c>x is null</c> / <c>x is not null</c>. The check stays syntactic on the
/// hot path and only consults semantic information after it has found a candidate, where it
/// suppresses diagnostics inside expression-tree lambdas because pattern matching is not legal
/// there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferIsNullPatternAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.PreferIsNullPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var expressionType = start.Compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, expressionType), SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        });
    }

    /// <summary>Returns whether the comparison checks one operand against the <c>null</c> literal.</summary>
    /// <param name="binary">The comparison expression.</param>
    /// <param name="operand">The non-null operand when matched.</param>
    /// <returns><see langword="true"/> when exactly one side is the <c>null</c> literal.</returns>
    internal static bool TryGetNullComparison(BinaryExpressionSyntax binary, out ExpressionSyntax? operand)
    {
        var leftNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
        var rightNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
        if (leftNull == rightNull)
        {
            operand = null;
            return false;
        }

        operand = leftNull ? binary.Right : binary.Left;
        return true;
    }

    /// <summary>Returns the preferred null-check pattern text for a comparison kind.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns><c>is null</c> or <c>is not null</c>.</returns>
    internal static string PatternText(SyntaxKind kind)
        => kind == SyntaxKind.NotEqualsExpression ? "is not null" : "is null";

    /// <summary>Reports SST1149 when a null comparison can be rewritten as an <c>is</c>-pattern.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expressionType">The resolved <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> definition, if any.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryGetNullComparison(binary, out _))
        {
            return;
        }

        if (expressionType is not null && IsInExpressionTree(binary, context.SemanticModel, expressionType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.PreferIsNullPattern, binary.GetLocation(), PatternText(binary.Kind())));
    }

    /// <summary>Returns whether the comparison appears inside a lambda converted to an expression tree.</summary>
    /// <param name="node">A node inside the candidate null comparison.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="expressionType">The resolved <c>Expression&lt;TDelegate&gt;</c> definition.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the containing lambda is an expression tree.</returns>
    private static bool IsInExpressionTree(SyntaxNode node, SemanticModel model, INamedTypeSymbol expressionType, CancellationToken cancellationToken)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is not AnonymousFunctionExpressionSyntax anonymous)
            {
                continue;
            }

            if (model.GetTypeInfo(anonymous, cancellationToken).ConvertedType is INamedTypeSymbol converted
                && SymbolEqualityComparer.Default.Equals(converted.OriginalDefinition, expressionType))
            {
                return true;
            }
        }

        return false;
    }
}
