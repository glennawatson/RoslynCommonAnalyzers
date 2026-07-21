// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>|</c> combination of <c>[Flags]</c> enum members where one operand's bits are all
/// already set by another operand in the same expression (SST2495), so it contributes nothing. Because
/// every operand's value is a compile-time constant, the redundancy is proven, not guessed.
/// </summary>
/// <remarks>
/// The clean path binds one operand at most: only the outermost operation of a <c>|</c> chain is
/// examined, and a result type that is not a <c>[Flags]</c> enum settles it after a single bind. Only a
/// combination that clears that has its operands' constant values read.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2495RedundantFlagsOperandAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute marking an enum as a flag set.</summary>
    private const string FlagsAttributeName = "FlagsAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.RedundantFlagsOperand);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.BitwiseOrExpression);
    }

    /// <summary>Reports each operand of a flags combination whose bits another operand already sets.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var expression = (BinaryExpressionSyntax)context.Node;
        if (IsInsideBitwiseOr(expression))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            || !HasFlagsAttribute(enumType))
        {
            return;
        }

        var count = CountOperands(expression);
        var operands = new ExpressionSyntax[count];
        var filled = 0;
        Flatten(expression, operands, ref filled);

        var bits = new ulong[count];
        var known = new bool[count];
        for (var i = 0; i < count; i++)
        {
            var constant = context.SemanticModel.GetConstantValue(operands[i], context.CancellationToken);
            known[i] = constant.HasValue && TryGetBits(constant.Value, out bits[i]) && bits[i] != 0;
        }

        for (var i = 0; i < count; i++)
        {
            if (known[i] && CoveringOperand(bits, known, count, i) is { } j)
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.RedundantFlagsOperand,
                    expression.SyntaxTree,
                    operands[i].Span,
                    operands[i].ToString(),
                    operands[j].ToString()));
            }
        }
    }

    /// <summary>Finds another operand whose bits already include operand <paramref name="i"/>'s bits.</summary>
    /// <param name="bits">Each operand's constant bit pattern.</param>
    /// <param name="known">Whether each operand's bits are a known non-zero constant.</param>
    /// <param name="count">The operand count.</param>
    /// <param name="i">The operand being tested for redundancy.</param>
    /// <returns>The covering operand's index, or <see langword="null"/> when the operand adds bits of its own.</returns>
    /// <remarks>
    /// When two operands set the same bits, only the later one is reported, so removing exactly the reported
    /// operands leaves the combined value unchanged.
    /// </remarks>
    private static int? CoveringOperand(ulong[] bits, bool[] known, int count, int i)
    {
        for (var j = 0; j < count; j++)
        {
            if (j == i || !known[j] || (bits[i] & bits[j]) != bits[i])
            {
                continue;
            }

            if (bits[i] != bits[j] || i > j)
            {
                return j;
            }
        }

        return null;
    }

    /// <summary>Counts the operands of a <c>|</c> chain, looking through parentheses.</summary>
    /// <param name="expression">The expression to count.</param>
    /// <returns>The number of leaf operands.</returns>
    private static int CountOperands(ExpressionSyntax expression)
    {
        expression = Unwrap(expression);
        return expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.BitwiseOrExpression)
            ? CountOperands(binary.Left) + CountOperands(binary.Right)
            : 1;
    }

    /// <summary>Fills the leaf operands of a <c>|</c> chain in source order, looking through parentheses.</summary>
    /// <param name="expression">The expression to flatten.</param>
    /// <param name="operands">The destination array.</param>
    /// <param name="index">The next free slot in the destination array.</param>
    private static void Flatten(ExpressionSyntax expression, ExpressionSyntax[] operands, ref int index)
    {
        expression = Unwrap(expression);
        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.BitwiseOrExpression))
        {
            Flatten(binary.Left, operands, ref index);
            Flatten(binary.Right, operands, ref index);
            return;
        }

        operands[index] = expression;
        index++;
    }

    /// <summary>Strips enclosing parentheses from an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Returns whether a <c>|</c> operation is itself an operand of a larger <c>|</c> operation.</summary>
    /// <param name="node">The operation being analyzed.</param>
    /// <returns><see langword="true"/> when an enclosing <c>|</c> already carries the whole chain.</returns>
    private static bool IsInsideBitwiseOr(SyntaxNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ParenthesizedExpressionSyntax)
            {
                continue;
            }

            return parent.RawKind == (int)SyntaxKind.BitwiseOrExpression;
        }

        return false;
    }

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

    /// <summary>Reads an enum operand's constant value as a bit pattern.</summary>
    /// <param name="value">The operand's constant value, boxed as its underlying integral type.</param>
    /// <param name="bits">The constant's bit pattern.</param>
    /// <returns><see langword="true"/> when the value is an integral constant.</returns>
    private static bool TryGetBits(object? value, out ulong bits)
    {
        switch (value)
        {
            case int number:
            {
                bits = unchecked((ulong)(long)number);
                return true;
            }

            case uint number:
            {
                bits = number;
                return true;
            }

            case long number:
            {
                bits = unchecked((ulong)number);
                return true;
            }

            case ulong number:
            {
                bits = number;
                return true;
            }

            case short number:
            {
                bits = unchecked((ulong)(long)number);
                return true;
            }

            case ushort number:
            {
                bits = number;
                return true;
            }

            case sbyte number:
            {
                bits = unchecked((ulong)(long)number);
                return true;
            }

            case byte number:
            {
                bits = number;
                return true;
            }

            default:
            {
                bits = 0;
                return false;
            }
        }
    }
}
