// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a bitwise operation whose constant operand cannot change the result (SST1481): <c>x | 0</c>,
/// <c>x ^ 0</c> and <c>x &amp; ~0</c> all return <c>x</c>, and <c>x &amp; 0</c> always returns <c>0</c>.
/// </summary>
/// <remarks>
/// <para>
/// The operation reads as though it does something, so the constant is usually the wrong one — a mask that
/// was meant to be non-zero, or a flag that was meant to be set. <c>x &amp; 0</c> is reported for the same
/// reason but carries no code fix: whether the author meant a different mask or meant to drop the
/// expression is not something the analyzer can know, and folding it to <c>0</c> would cement the bug.
/// </para>
/// <para>
/// A zero shift — <c>x &lt;&lt; 0</c>, <c>x &gt;&gt; 0</c> — is the same kind of mistake and is owned by
/// SST1478, which knows the operand's width and can say what the shift actually does. Shifts are not
/// registered here, so the two rules never report the same expression.
/// </para>
/// <para>
/// "All bits set" is decided against the type the operation runs on, not the type of the operand as
/// written. The built-in <c>&amp;</c>, <c>|</c> and <c>^</c> promote their operands, so a <c>byte</c> masked
/// with <c>0xFF</c> is an <c>int</c> operation whose all-bits-set constant is <c>-1</c>. That is why
/// <c>b &amp; 0xFF</c> is left alone, which is what the people who write it as a defensive narrowing expect.
/// Booleans are not integral and belong to SST1468; an enum has no special type and is not reported.
/// </para>
/// <para>
/// Ordered so the clean path is two cheap rejections. An operand that is a call, an index or a new object
/// can never be constant, and an operation with no constant-shaped operand is dropped on syntax alone. The
/// operation is then bound once to learn its type, which throws out every boolean, enum and user-defined
/// operator before a constant is ever asked for.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1481RedundantBitwiseOperationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property naming the operand that survives the code fix.</summary>
    internal const string SurvivingOperandKey = "SurvivingOperand";

    /// <summary>The <see cref="SurvivingOperandKey"/> value for a redundant right operand.</summary>
    internal const string LeftOperandSurvives = "left";

    /// <summary>The <see cref="SurvivingOperandKey"/> value for a redundant left operand.</summary>
    internal const string RightOperandSurvives = "right";

    /// <summary>The mask of a 32-bit operation, used to compare a constant against all bits set.</summary>
    private const ulong NarrowMask = uint.MaxValue;

    /// <summary>The mask of a 64-bit operation.</summary>
    private const ulong WideMask = ulong.MaxValue;

    /// <summary>The properties attached when the left operand survives.</summary>
    private static readonly ImmutableDictionary<string, string?> LeftSurvivesProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SurvivingOperandKey, LeftOperandSurvives);

    /// <summary>The properties attached when the right operand survives.</summary>
    private static readonly ImmutableDictionary<string, string?> RightSurvivesProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SurvivingOperandKey, RightOperandSurvives);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(MaintainabilityRules.RedundantBitwiseOperation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.BitwiseOrExpression,
            SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.BitwiseAndExpression);
    }

    /// <summary>Reports one bitwise operation whose constant operand leaves the result decided.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!CanBeConstant(binary.Left) && !CanBeConstant(binary.Right))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (GetValueMask(model.GetTypeInfo(binary, cancellationToken).Type) is not { } mask)
        {
            return;
        }

        var isAnd = binary.RawKind == (int)SyntaxKind.BitwiseAndExpression;
        if (IsIdentityOperand(binary.Right, mask, isAnd, model, cancellationToken, out var alwaysZero))
        {
            Report(context, binary, alwaysZero ? null : LeftSurvivesProperties);
            return;
        }

        if (!IsIdentityOperand(binary.Left, mask, isAnd, model, cancellationToken, out alwaysZero))
        {
            return;
        }

        Report(context, binary, alwaysZero ? null : RightSurvivesProperties);
    }

    /// <summary>Reports the operation, naming the operand a fix would keep.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="binary">The reported operation.</param>
    /// <param name="properties">The surviving-operand properties, or <see langword="null"/> when there is no fix.</param>
    private static void Report(SyntaxNodeAnalysisContext context, BinaryExpressionSyntax binary, ImmutableDictionary<string, string?>? properties)
    {
        var text = binary.ToString();
        context.ReportDiagnostic(properties is null
            ? DiagnosticHelper.Create(MaintainabilityRules.RedundantBitwiseOperation, binary.SyntaxTree, binary.Span, text)
            : DiagnosticHelper.Create(MaintainabilityRules.RedundantBitwiseOperation, binary.SyntaxTree, binary.Span, properties, text));
    }

    /// <summary>Returns whether an operand is a constant that decides the operation's result.</summary>
    /// <param name="operand">The operand to classify.</param>
    /// <param name="mask">All bits set for the type the operation runs on.</param>
    /// <param name="isAnd">Whether the operation is <c>&amp;</c>.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="alwaysZero">Whether the operand forces the result to zero rather than leaving it alone.</param>
    /// <returns><see langword="true"/> when the operation cannot mean what it says.</returns>
    private static bool IsIdentityOperand(
        ExpressionSyntax operand,
        ulong mask,
        bool isAnd,
        SemanticModel model,
        CancellationToken cancellationToken,
        out bool alwaysZero)
    {
        alwaysZero = false;
        if (!CanBeConstant(operand))
        {
            return false;
        }

        var constant = model.GetConstantValue(operand, cancellationToken);
        if (!constant.HasValue || !TryGetBits(constant.Value, mask, out var bits))
        {
            return false;
        }

        if (!isAnd)
        {
            // `x | 0` and `x ^ 0` both hand back x untouched.
            return bits == 0;
        }

        if (bits == 0)
        {
            // `x & 0` is always 0, whatever x is.
            alwaysZero = true;
            return true;
        }

        // `x & ~0` keeps every bit of x.
        return bits == mask;
    }

    /// <summary>Reads a constant operand's bit pattern, narrowed to the operation's width.</summary>
    /// <param name="value">The operand's constant value.</param>
    /// <param name="mask">All bits set for the type the operation runs on.</param>
    /// <param name="bits">The constant's bit pattern.</param>
    /// <returns><see langword="true"/> when the constant is an integral value.</returns>
    /// <remarks>
    /// A signed constant is sign-extended before it is narrowed, so the <c>int</c> literal <c>-1</c> in
    /// <c>longValue &amp; -1</c> reads as all 64 bits set, which is what the compiler makes of it.
    /// </remarks>
    private static bool TryGetBits(object? value, ulong mask, out ulong bits)
    {
        switch (value)
        {
            case int number:
            {
                bits = unchecked((ulong)(long)number);
                break;
            }

            case uint number:
            {
                bits = number;
                break;
            }

            case long number:
            {
                bits = unchecked((ulong)number);
                break;
            }

            case ulong number:
            {
                bits = number;
                break;
            }

            case short number:
            {
                bits = unchecked((ulong)(long)number);
                break;
            }

            case ushort number:
            {
                bits = number;
                break;
            }

            case sbyte number:
            {
                bits = unchecked((ulong)(long)number);
                break;
            }

            case byte number:
            {
                bits = number;
                break;
            }

            case char number:
            {
                bits = number;
                break;
            }

            default:
            {
                bits = 0;
                return false;
            }
        }

        bits &= mask;
        return true;
    }

    /// <summary>Gets all bits set for the type a bitwise operation runs on.</summary>
    /// <param name="type">The operation's result type.</param>
    /// <returns>The mask, or <see langword="null"/> when the rule does not examine the type.</returns>
    /// <remarks>
    /// Operand promotion leaves only four possible result types for a built-in integral <c>&amp;</c>,
    /// <c>|</c> or <c>^</c>. A boolean operation is SST1468's; an enum, a native integer and a user-defined
    /// operator all fall out here, the native integer because its width is a property of the process.
    /// </remarks>
    private static ulong? GetValueMask(ITypeSymbol? type) => type?.SpecialType switch
    {
        SpecialType.System_Int32 or SpecialType.System_UInt32 => NarrowMask,
        SpecialType.System_Int64 or SpecialType.System_UInt64 => WideMask,
        _ => null,
    };

    /// <summary>Returns whether an expression's shape allows it to be a compile-time constant.</summary>
    /// <param name="expression">The operand to inspect.</param>
    /// <returns><see langword="true"/> when the operand could bind to a constant.</returns>
    /// <remarks>
    /// A name can be a <c>const</c>, and a literal, a sign, a <c>~</c>, a cast and an operation over those
    /// all fold. A call, an index, a <c>new</c> and everything else cannot, and is rejected before the
    /// semantic model is asked anything.
    /// </remarks>
    private static bool CanBeConstant(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax => true,
        IdentifierNameSyntax => true,
        MemberAccessExpressionSyntax => true,
        ParenthesizedExpressionSyntax parenthesized => CanBeConstant(parenthesized.Expression),
        PrefixUnaryExpressionSyntax prefix => CanBeConstant(prefix.Operand),
        CastExpressionSyntax cast => CanBeConstant(cast.Expression),
        BinaryExpressionSyntax binary => CanBeConstant(binary.Left) && CanBeConstant(binary.Right),
        _ => false,
    };
}
