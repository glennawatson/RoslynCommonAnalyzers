// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an integer division whose truncated result is then widened to <c>float</c>, <c>double</c> or
/// <c>decimal</c> (SST1477). The division happens first and throws the remainder away, so <c>1 / 2</c>
/// stored in a <see cref="double"/> is <c>0</c>, not <c>0.5</c>; widening afterwards cannot bring the
/// remainder back.
/// </summary>
/// <remarks>
/// <para>
/// The widening context is not enumerated shape by shape. Every one of them — an initializer, a
/// <c>return</c> in a floating-point member, an argument bound to a floating-point parameter, a compound
/// assignment, a branch of a conditional whose common type is floating point — is the same fact in the
/// semantic model: the division's converted type is a floating-point type while its own type is integral.
/// Asking that one question covers them all, and covers the ones nobody thought to list. An explicit
/// <c>(double)(a / b)</c> is the exception, because the conversion sits on the cast rather than on the
/// division, so the cast's target is read directly.
/// </para>
/// <para>
/// Ordered so the clean path costs one bind. A real literal on either operand is rejected on the token's
/// text, without the model. Otherwise the unwrapped division is bound once, which yields both its own type
/// and its converted type; a division that is not being widened stops there, and only a division that is
/// actually widened pays for anything more.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1477IntegerDivisionAsFloatingPointAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property that carries the floating-point type the code fix casts to.</summary>
    internal const string TargetTypeKey = "TargetType";

    /// <summary>The C# keyword for <see cref="SpecialType.System_Single"/>.</summary>
    internal const string FloatName = "float";

    /// <summary>The C# keyword for <see cref="SpecialType.System_Double"/>.</summary>
    internal const string DoubleName = "double";

    /// <summary>The C# keyword for <see cref="SpecialType.System_Decimal"/>.</summary>
    internal const string DecimalName = "decimal";

    /// <summary>The properties attached to a division widened to <c>float</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> FloatProperties =
        ImmutableDictionary<string, string?>.Empty.Add(TargetTypeKey, FloatName);

    /// <summary>The properties attached to a division widened to <c>double</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> DoubleProperties =
        ImmutableDictionary<string, string?>.Empty.Add(TargetTypeKey, DoubleName);

    /// <summary>The properties attached to a division widened to <c>decimal</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> DecimalProperties =
        ImmutableDictionary<string, string?>.Empty.Add(TargetTypeKey, DecimalName);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(MaintainabilityRules.IntegerDivisionAsFloatingPoint);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.DivideExpression);
    }

    /// <summary>Reports one division whose integral result is consumed as a floating-point value.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var division = (BinaryExpressionSyntax)context.Node;

        // A real literal on either side means the division already runs in floating point, so nothing
        // truncates. Rejecting it on the token's text keeps the model out of the commonest clean shape.
        if (IsRealLiteral(division.Left) || IsRealLiteral(division.Right))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;

        // Parentheses and a unary sign do not change the value, so the conversion — when there is one —
        // lands on the outermost of them, not on the division itself.
        var widened = Unwrap(division);
        var info = model.GetTypeInfo(widened, cancellationToken);
        var target = widened.Parent is CastExpressionSyntax cast
            ? GetFloatingName(model.GetTypeInfo(cast.Type, cancellationToken).Type)
            : GetFloatingName(info.ConvertedType);
        if (target is null)
        {
            return;
        }

        // The division only truncates when it is itself integral; `(double)a / b` is already exact.
        var divisionType = ReferenceEquals(widened, division) ? info.Type : model.GetTypeInfo(division, cancellationToken).Type;
        if (!IsIntegral(divisionType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.IntegerDivisionAsFloatingPoint,
            division.SyntaxTree,
            division.Span,
            GetProperties(target),
            target));
    }

    /// <summary>Walks out through the wrappers that leave a division's value unchanged.</summary>
    /// <param name="division">The division expression.</param>
    /// <returns>The outermost node that still denotes the truncated quotient.</returns>
    /// <remarks>
    /// A conversion is recorded against the whole expression it applies to, so <c>double d = -(a / b);</c>
    /// carries it on the negation, not on the division buried inside it.
    /// </remarks>
    private static ExpressionSyntax Unwrap(BinaryExpressionSyntax division)
    {
        ExpressionSyntax node = division;
        while (node.Parent is ExpressionSyntax parent && IsQuotientWrapper(parent))
        {
            node = parent;
        }

        return node;
    }

    /// <summary>Returns whether a parent expression still denotes the quotient its operand produced.</summary>
    /// <param name="parent">The expression wrapping the quotient.</param>
    /// <returns><see langword="true"/> for parentheses and for a unary sign.</returns>
    /// <remarks>Neither rounds, so neither is where a conversion to floating point would be recorded.</remarks>
    private static bool IsQuotientWrapper(ExpressionSyntax parent) => parent.RawKind
        is (int)SyntaxKind.ParenthesizedExpression
        or (int)SyntaxKind.UnaryMinusExpression
        or (int)SyntaxKind.UnaryPlusExpression;

    /// <summary>Returns whether an operand is a literal written as a real number.</summary>
    /// <param name="operand">The operand to inspect.</param>
    /// <returns><see langword="true"/> when the literal's own type is floating point.</returns>
    private static bool IsRealLiteral(ExpressionSyntax operand)
        => operand is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal
            && IsRealLiteralText(literal.Token.Text);

    /// <summary>Returns whether a numeric literal's source text spells a real number.</summary>
    /// <param name="text">The literal's source text.</param>
    /// <returns><see langword="true"/> for a decimal point, an exponent, or a real suffix.</returns>
    /// <remarks>
    /// A hexadecimal or binary literal is always integral, and is rejected first so its digits — which can
    /// include <c>e</c>, <c>d</c> and <c>f</c> — are never mistaken for an exponent or a suffix.
    /// </remarks>
    private static bool IsRealLiteralText(string text)
    {
        if (IsBitPatternText(text))
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (IsRealMarker(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a numeric literal is written as a hexadecimal or binary bit pattern.</summary>
    /// <param name="text">The literal's source text.</param>
    /// <returns><see langword="true"/> for a <c>0x</c> or <c>0b</c> prefix.</returns>
    private static bool IsBitPatternText(string text)
        => text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X' or 'b' or 'B';

    /// <summary>Returns whether a character can only appear in a literal that spells a real number.</summary>
    /// <param name="character">One character of the literal's source text.</param>
    /// <returns><see langword="true"/> for a decimal point, an exponent marker, or a real suffix.</returns>
    private static bool IsRealMarker(char character)
        => character is '.' or 'e' or 'E' or 'f' or 'F' or 'd' or 'D' or 'm' or 'M';

    /// <summary>Returns whether a type divides by truncating toward zero.</summary>
    /// <param name="type">The division's own type.</param>
    /// <returns><see langword="true"/> for the built-in integral types the division operator yields.</returns>
    /// <remarks>
    /// The built-in <c>/</c> promotes its operands, so its result can only be <c>int</c>, <c>uint</c>,
    /// <c>long</c>, <c>ulong</c> or a native integer. A user-defined <c>/</c> — on <c>BigInteger</c>, say —
    /// has no special type and is left alone, because this rule cannot know whether it truncates.
    /// </remarks>
    private static bool IsIntegral(ITypeSymbol? type) => type?.SpecialType
        is SpecialType.System_Int32
        or SpecialType.System_UInt32
        or SpecialType.System_Int64
        or SpecialType.System_UInt64
        or SpecialType.System_IntPtr
        or SpecialType.System_UIntPtr;

    /// <summary>Gets the C# keyword for a floating-point type.</summary>
    /// <param name="type">The type the quotient is converted to.</param>
    /// <returns>The keyword, or <see langword="null"/> when the type is not floating point.</returns>
    private static string? GetFloatingName(ITypeSymbol? type) => type?.SpecialType switch
    {
        SpecialType.System_Single => FloatName,
        SpecialType.System_Double => DoubleName,
        SpecialType.System_Decimal => DecimalName,
        _ => null,
    };

    /// <summary>Gets the cached diagnostic properties for a floating-point target.</summary>
    /// <param name="target">The target type's keyword.</param>
    /// <returns>The properties the code fix reads the cast type from.</returns>
    private static ImmutableDictionary<string, string?> GetProperties(string target) => target switch
    {
        FloatName => FloatProperties,
        DecimalName => DecimalProperties,
        _ => DoubleProperties,
    };
}
