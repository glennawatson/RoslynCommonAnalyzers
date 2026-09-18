// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Checks newly constant integer arithmetic without allocating replacement syntax or a compilation.</summary>
internal static class ConstInt32Arithmetic
{
    /// <summary>The maximum arithmetic nesting proved safe in one analysis.</summary>
    private const int MaximumExpressionDepth = 32;

    /// <summary>Checks whether an integer expression has a representable constant result.</summary>
    /// <param name="expression">The arithmetic expression whose locals are known to be constant candidates.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    /// <returns>Whether every operation has a valid Int32 result.</returns>
    internal static bool IsSafe(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken) =>
        model.GetTypeInfo(expression, cancellationToken).Type?.SpecialType == SpecialType.System_Int32
            && GetValue(expression, model, cancellationToken).HasValue;

    /// <summary>Evaluates supported integer syntax and resolves existing constant initializers.</summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    /// <param name="depth">The current expression nesting depth.</param>
    /// <returns>The constant value, or null when folding is not proved safe.</returns>
    private static int? GetValue(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken, int depth = 0) =>
        depth >= MaximumExpressionDepth ? null : GetValueCore(expression, model, depth + 1, cancellationToken);

    /// <summary>Evaluates one bounded level of integer syntax.</summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="depth">The depth to pass to child expressions.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    /// <returns>The integer result, or null.</returns>
    private static int? GetValueCore(ExpressionSyntax expression, SemanticModel model, int depth, CancellationToken cancellationToken) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => GetValue(parenthesized.Expression, model, cancellationToken, depth),
        CheckedExpressionSyntax checkedExpression => GetValue(checkedExpression.Expression, model, cancellationToken, depth),
        BinaryExpressionSyntax binary => GetBinaryValue(binary, model, depth, cancellationToken),
        PrefixUnaryExpressionSyntax prefix => GetPrefixValue(prefix, model, depth, cancellationToken),
        IdentifierNameSyntax identifier => GetIdentifierValue(identifier, model, cancellationToken),
        _ => model.GetConstantValue(expression, cancellationToken) is { HasValue: true, Value: var value } ? GetInt32(value) : null,
    };

    /// <summary>Resolves a constant or a candidate local's constant initializer.</summary>
    /// <param name="identifier">The referenced value.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    /// <returns>The initializer's integer value, when available.</returns>
    private static int? GetIdentifierValue(IdentifierNameSyntax identifier, SemanticModel model, CancellationToken cancellationToken)
    {
        var constant = model.GetConstantValue(identifier, cancellationToken);
        if (constant.HasValue)
        {
            return GetInt32(constant.Value);
        }

        return model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol { DeclaringSyntaxReferences.Length: 1 } local
            && local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is VariableDeclaratorSyntax { Initializer.Value: { } initializer }
            && model.GetConstantValue(initializer, cancellationToken) is { HasValue: true, Value: var value }
            ? GetInt32(value)
            : null;
    }

    /// <summary>Checks both operands before evaluating a binary operation.</summary>
    /// <param name="binary">The binary expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="depth">The depth to pass to the operands.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    /// <returns>The representable result, or null.</returns>
    private static int? GetBinaryValue(BinaryExpressionSyntax binary, SemanticModel model, int depth, CancellationToken cancellationToken) =>
        GetValue(binary.Left, model, cancellationToken, depth) is { } left && GetValue(binary.Right, model, cancellationToken, depth) is { } right
            ? EvaluateBinary(binary.Kind(), left, right)
            : null;

    /// <summary>Uses wide intermediates to check integer overflow without throwing.</summary>
    /// <param name="kind">The arithmetic operator.</param>
    /// <param name="left">The first operand.</param>
    /// <param name="right">The second operand.</param>
    /// <returns>The representable result, or null.</returns>
    private static int? EvaluateBinary(SyntaxKind kind, int left, int right) => kind switch
    {
        SyntaxKind.AddExpression => InRange((long)left + right),
        SyntaxKind.SubtractExpression => InRange((long)left - right),
        SyntaxKind.MultiplyExpression => InRange((long)left * right),
        SyntaxKind.DivideExpression => Divide(left, right, remainder: false),
        SyntaxKind.ModuloExpression => Divide(left, right, remainder: true),
        _ => EvaluateBitwise(kind, left, right),
    };

    /// <summary>Evaluates bitwise operators whose results always fit the operand type.</summary>
    /// <param name="kind">The bitwise operator.</param>
    /// <param name="left">The first operand.</param>
    /// <param name="right">The second operand.</param>
    /// <returns>The result, or null for another operator.</returns>
    private static int? EvaluateBitwise(SyntaxKind kind, int left, int right) => kind switch
    {
        SyntaxKind.BitwiseAndExpression => left & right,
        SyntaxKind.BitwiseOrExpression => left | right,
        SyntaxKind.ExclusiveOrExpression => left ^ right,
        SyntaxKind.LeftShiftExpression => left << right,
        SyntaxKind.RightShiftExpression => left >> right,
        SyntaxKind.UnsignedRightShiftExpression => left >>> right,
        _ => null,
    };

    /// <summary>Rejects division that can throw or fail constant evaluation.</summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <param name="remainder">Whether to evaluate remainder rather than quotient.</param>
    /// <returns>The result, or null when division is invalid.</returns>
    private static int? Divide(int left, int right, bool remainder)
    {
        if (right == 0 || (left == int.MinValue && right == -1))
        {
            return null;
        }

        return remainder ? left % right : left / right;
    }

    /// <summary>Checks unary integer operations.</summary>
    /// <param name="prefix">The unary expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="depth">The depth to pass to the operand.</param>
    /// <param name="cancellationToken">The analysis cancellation token.</param>
    /// <returns>The representable result, or null.</returns>
    private static int? GetPrefixValue(PrefixUnaryExpressionSyntax prefix, SemanticModel model, int depth, CancellationToken cancellationToken) =>
        GetValue(prefix.Operand, model, cancellationToken, depth) is { } value
            ? prefix.Kind() switch
            {
                SyntaxKind.UnaryPlusExpression => value,
                SyntaxKind.UnaryMinusExpression => InRange(-(long)value),
                SyntaxKind.BitwiseNotExpression => ~value,
                _ => null,
            }
            : null;

    /// <summary>Extracts values whose implicit numeric promotion produces Int32.</summary>
    /// <param name="value">The compiler's boxed constant value.</param>
    /// <returns>The integer value, or null for another constant type.</returns>
    private static int? GetInt32(object? value) => value switch
    {
        int number => number,
        short number => number,
        ushort number => number,
        sbyte number => number,
        byte number => number,
        char character => character,
        _ => null,
    };

    /// <summary>Checks a wide intermediate against the Int32 range.</summary>
    /// <param name="value">The intermediate result.</param>
    /// <returns>The representable integer, or null.</returns>
    private static int? InRange(long value) => value is >= int.MinValue and <= int.MaxValue ? (int)value : null;
}
