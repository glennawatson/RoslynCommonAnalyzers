// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a bitwise operation — <c>|</c>, <c>&amp;</c>, <c>^</c>, <c>~</c> and their compound
/// assignments — whose operand is an enum not declared as a flag set (SST2458).
/// </summary>
/// <remarks>
/// <para>
/// An enum without <c>[Flags]</c> numbers its members sequentially, so they share bits: or-ing two of
/// them lands on a third member or on no member at all, and a masked test examines bits nobody
/// assigned. A masked value compared to a named member, or tested against zero, is reported for the
/// same reason — without one bit per member the comparison answers the wrong question. If the enum
/// really is a bit set, the repair is to say so with <c>[Flags]</c> and per-bit values.
/// </para>
/// <para>
/// A chain of bitwise operations over the same enum is one mistake, not several, so only the
/// outermost operation of the chain is reported. An enum explicitly cast to a numeric type is a raw
/// number by declaration, and arithmetic over such casts is not reported; equality comparisons are
/// the supported way to use a non-flags enum and are never examined.
/// </para>
/// <para>
/// The clean path binds one operand at most. Only the bitwise operator kinds are registered, a
/// literal or numeric-cast operand is rejected on syntax alone, and for a binary operation whose
/// left operand binds to a non-enum type the right operand is never bound — code that compiles
/// cannot pair an enum with it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2458NonFlagsEnumBitwiseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute marking an enum as a flag set.</summary>
    private const string FlagsAttributeName = "FlagsAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.NonFlagsEnumBitwise);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeBinary,
            SyntaxKind.BitwiseOrExpression,
            SyntaxKind.BitwiseAndExpression,
            SyntaxKind.ExclusiveOrExpression);
        context.RegisterSyntaxNodeAction(AnalyzeComplement, SyntaxKind.BitwiseNotExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeAssignment,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression);
    }

    /// <summary>Reports one binary bitwise operation whose operand is a non-flags enum.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeBinary(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (IsInsideBitwiseOperation(binary)
            || ResolveNonFlagsEnumOperand(context, binary.Left, binary.Right) is not { } enumType)
        {
            return;
        }

        Report(context, binary, enumType, binary.OperatorToken.Text);
    }

    /// <summary>Reports one complement whose operand is a non-flags enum.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeComplement(SyntaxNodeAnalysisContext context)
    {
        var complement = (PrefixUnaryExpressionSyntax)context.Node;
        if (IsInsideBitwiseOperation(complement)
            || IsNumericByShape(complement.Operand)
            || ResolveNonFlagsEnum(context, complement.Operand) is not { } enumType)
        {
            return;
        }

        Report(context, complement, enumType, complement.OperatorToken.Text);
    }

    /// <summary>Reports one compound bitwise assignment whose target is a non-flags enum.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (IsInsideBitwiseOperation(assignment)
            || ResolveNonFlagsEnum(context, assignment.Left) is not { } enumType)
        {
            return;
        }

        Report(context, assignment, enumType, assignment.OperatorToken.Text);
    }

    /// <summary>Reports the operation, naming the enum and the operator that misreads it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="operation">The reported operation.</param>
    /// <param name="enumType">The non-flags enum an operand belongs to.</param>
    /// <param name="operatorText">The operator's source text.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxNode operation, INamedTypeSymbol enumType, string operatorText)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.NonFlagsEnumBitwise,
            operation.SyntaxTree,
            operation.Span,
            enumType.Name,
            operatorText));

    /// <summary>Finds the non-flags enum a binary operation works on, binding as little as it can.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The non-flags enum type, or <see langword="null"/> when the operation is not the bug.</returns>
    /// <remarks>
    /// The right operand is bound only when the left one says nothing: a literal or a numeric cast on
    /// the left (<c>0 | e</c>) leaves the enum on the right, and so does a left operand that fails to
    /// bind. A left operand that binds to a valid non-enum type settles it — no built-in bitwise
    /// operator pairs an enum with anything but itself, the sole exception being the literal
    /// <c>0</c>, which the shape check already routed past the bind.
    /// </remarks>
    private static INamedTypeSymbol? ResolveNonFlagsEnumOperand(SyntaxNodeAnalysisContext context, ExpressionSyntax left, ExpressionSyntax right)
    {
        if (!IsNumericByShape(left))
        {
            var leftType = context.SemanticModel.GetTypeInfo(left, context.CancellationToken).Type;
            if (GetEnumType(leftType) is { } leftEnum)
            {
                return HasFlagsAttribute(leftEnum) ? null : leftEnum;
            }

            if (leftType is not null && leftType.TypeKind != TypeKind.Error)
            {
                return null;
            }
        }

        if (IsNumericByShape(right))
        {
            return null;
        }

        return ResolveNonFlagsEnum(context, right);
    }

    /// <summary>Binds one expression and returns its type when that is a non-flags enum.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The expression to bind.</param>
    /// <returns>The non-flags enum type, or <see langword="null"/> for every other type.</returns>
    private static INamedTypeSymbol? ResolveNonFlagsEnum(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        => GetEnumType(context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type) is { } enumType
            && !HasFlagsAttribute(enumType)
            ? enumType
            : null;

    /// <summary>Gets the enum behind a type, looking through the nullable wrapper the lifted operators use.</summary>
    /// <param name="type">The operand's type.</param>
    /// <returns>The enum type, or <see langword="null"/> when the operand is not an enum at all.</returns>
    private static INamedTypeSymbol? GetEnumType(ITypeSymbol? type) => type switch
    {
        INamedTypeSymbol { TypeKind: TypeKind.Enum } named => named,
        INamedTypeSymbol named when named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1
            && named.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Enum } lifted => lifted,
        _ => null,
    };

    /// <summary>Returns whether an enum is declared as a flag set.</summary>
    /// <param name="enumType">The enum type.</param>
    /// <returns><see langword="true"/> when the enum carries <c>System.FlagsAttribute</c>.</returns>
    private static bool HasFlagsAttribute(INamedTypeSymbol enumType)
    {
        var attributes = enumType.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass is { Name: FlagsAttributeName } attribute
                && attribute.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an operation is itself an operand of a larger bitwise operation.</summary>
    /// <param name="node">The operation being analyzed.</param>
    /// <returns><see langword="true"/> when an enclosing operation will carry the report.</returns>
    /// <remarks>
    /// <c>a | b | c</c> is one mistake. The inner operations stay silent and the outermost one
    /// reports the whole chain — an enum operand gives the chain the same enum type all the way up,
    /// so the outermost operation always sees it. A cast breaks the chain, and correctly so:
    /// <c>(int)(a | b) | mask</c> reports the enum combination inside the cast and leaves the
    /// numeric or around it alone.
    /// </remarks>
    private static bool IsInsideBitwiseOperation(SyntaxNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ParenthesizedExpressionSyntax)
            {
                continue;
            }

            return parent.RawKind
                is (int)SyntaxKind.BitwiseOrExpression
                or (int)SyntaxKind.BitwiseAndExpression
                or (int)SyntaxKind.ExclusiveOrExpression
                or (int)SyntaxKind.BitwiseNotExpression
                or (int)SyntaxKind.OrAssignmentExpression
                or (int)SyntaxKind.AndAssignmentExpression
                or (int)SyntaxKind.ExclusiveOrAssignmentExpression;
        }

        return false;
    }

    /// <summary>Returns whether an operand's shape already proves it is a number rather than an enum.</summary>
    /// <param name="expression">The operand to inspect.</param>
    /// <returns><see langword="true"/> when the operand cannot have an enum type.</returns>
    /// <remarks>
    /// A literal is never enum-typed, and a cast to a predefined type is the author saying "raw
    /// number" — the exact escape hatch the rule leaves open. Both are settled without the semantic
    /// model.
    /// </remarks>
    private static bool IsNumericByShape(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression is LiteralExpressionSyntax or CastExpressionSyntax { Type: PredefinedTypeSyntax };
    }
}
