// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports equality tests of a string against the empty string (PSH1204). A
/// comparison qualifies when one operand of <c>==</c>/<c>!=</c> is the literal
/// <c>""</c> or the <see cref="string.Empty"/> field, the other operand's type is
/// <see cref="string"/>, and the operator binds to string's built-in equality —
/// checking <c>Length</c> against zero is a field read instead of the full
/// string-equality path. Comparisons inside lambdas converted to
/// <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> are skipped because
/// query providers must translate <c>== ""</c> themselves.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1204EmptyStringComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the expression-tree delegate wrapper type.</summary>
    private const string ExpressionOfTMetadataName = "System.Linq.Expressions.Expression`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.EmptyStringComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var expressionOfTType = start.Compilation.GetTypeByMetadataName(ExpressionOfTMetadataName);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeComparison(nodeContext, expressionOfTType),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        });
    }

    /// <summary>Splits a comparison into its empty-string operand and its value operand, syntactically.</summary>
    /// <param name="binary">The <c>==</c>/<c>!=</c> expression to probe.</param>
    /// <param name="empty">The operand that is the literal <c>""</c> or a <c>.Empty</c> member access.</param>
    /// <param name="value">The other operand.</param>
    /// <param name="emptyIsLiteral"><see langword="true"/> when the empty operand is the literal <c>""</c>; a <c>.Empty</c> access still needs the semantic field check.</param>
    /// <returns><see langword="true"/> when one operand looks like the empty string. Literals win over
    /// <c>.Empty</c> accesses so a custom <c>.Empty</c> property compared to <c>""</c> is still recognized.</returns>
    internal static bool TryGetOperands(BinaryExpressionSyntax binary, out ExpressionSyntax? empty, out ExpressionSyntax? value, out bool emptyIsLiteral)
    {
        if (IsEmptyStringLiteral(binary.Left))
        {
            empty = binary.Left;
            value = binary.Right;
            emptyIsLiteral = true;
            return true;
        }

        if (IsEmptyStringLiteral(binary.Right))
        {
            empty = binary.Right;
            value = binary.Left;
            emptyIsLiteral = true;
            return true;
        }

        if (IsEmptyMemberAccess(binary.Left))
        {
            empty = binary.Left;
            value = binary.Right;
            emptyIsLiteral = false;
            return true;
        }

        if (IsEmptyMemberAccess(binary.Right))
        {
            empty = binary.Right;
            value = binary.Left;
            emptyIsLiteral = false;
            return true;
        }

        empty = null;
        value = null;
        emptyIsLiteral = false;
        return false;
    }

    /// <summary>Reports PSH1204 for a comparison of a string against the empty string.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expressionOfTType">The compilation's <c>Expression&lt;TDelegate&gt;</c> type, when it exists.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionOfTType)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryGetOperands(binary, out var empty, out var value, out var emptyIsLiteral))
        {
            return;
        }

        var model = context.SemanticModel;
        if ((!emptyIsLiteral && !IsStringEmptyField(model, empty!, context.CancellationToken))
            || model.GetTypeInfo(value!, context.CancellationToken).Type is not { SpecialType: SpecialType.System_String }
            || !IsStringEqualityOperator(model, binary, context.CancellationToken)
            || IsInsideExpressionTree(model, binary, expressionOfTType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.EmptyStringComparison,
            binary.SyntaxTree,
            binary.OperatorToken.Span));
    }

    /// <summary>Returns whether an expression is the literal <c>""</c>.</summary>
    /// <param name="expression">The candidate operand expression.</param>
    /// <returns><see langword="true"/> for a string literal whose value is empty.</returns>
    private static bool IsEmptyStringLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && literal.Token.ValueText.Length == 0;

    /// <summary>Returns whether an expression is a member access ending in <c>.Empty</c>, syntactically.</summary>
    /// <param name="expression">The candidate operand expression.</param>
    /// <returns><see langword="true"/> for a simple member access named <c>Empty</c>.</returns>
    private static bool IsEmptyMemberAccess(ExpressionSyntax expression)
        => expression is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Name is IdentifierNameSyntax { Identifier.ValueText: "Empty" };

    /// <summary>Returns whether a <c>.Empty</c> access binds to the <see cref="string.Empty"/> field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="empty">The <c>.Empty</c> member access to bind.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the access is the static <c>Empty</c> field on <see cref="string"/>.</returns>
    private static bool IsStringEmptyField(SemanticModel model, ExpressionSyntax empty, CancellationToken cancellationToken)
        => model.GetSymbolInfo(empty, cancellationToken).Symbol is IFieldSymbol
        {
            IsStatic: true,
            ContainingType.SpecialType: SpecialType.System_String
        };

    /// <summary>Returns whether the comparison binds to string's built-in equality operator.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="binary">The comparison expression to bind.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for string equality; a user-defined operator on another type is excluded.</returns>
    private static bool IsStringEqualityOperator(SemanticModel model, BinaryExpressionSyntax binary, CancellationToken cancellationToken)
        => model.GetSymbolInfo(binary, cancellationToken).Symbol is IMethodSymbol
        {
            ContainingType.SpecialType: SpecialType.System_String
        };

    /// <summary>Returns whether the comparison sits inside a lambda converted to an expression tree.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="node">The comparison node.</param>
    /// <param name="expressionOfTType">The compilation's <c>Expression&lt;TDelegate&gt;</c> type, when it exists.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when an enclosing anonymous function's converted type is constructed from <c>Expression&lt;TDelegate&gt;</c>.</returns>
    private static bool IsInsideExpressionTree(SemanticModel model, SyntaxNode node, INamedTypeSymbol? expressionOfTType, CancellationToken cancellationToken)
    {
        if (expressionOfTType is null)
        {
            return false;
        }

        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax function
                && model.GetTypeInfo(function, cancellationToken).ConvertedType is INamedTypeSymbol convertedType
                && SymbolEqualityComparer.Default.Equals(convertedType.ConstructedFrom, expressionOfTType))
            {
                return true;
            }

            if (current is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }
}
