// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a relational comparison (<c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>) whose result the
/// operand's integer type already fixes (SST2489). An unsigned value is never negative, so <c>x &gt;= 0</c> is
/// always true, <c>x &lt; 0</c> is always false, and <c>x &gt; 0</c> is an <c>!= 0</c> test in disguise; a value
/// compared to its own type's maximum is settled the same way, so <c>b &lt;= 255</c> is always true for a
/// <c>byte</c> and <c>b &gt; 255</c> always false. These read as range checks but guard nothing.
/// </summary>
/// <remarks>
/// <para>
/// Only a comparison whose constant sits exactly on the operand type's minimum or maximum is reported. A constant
/// past that edge is already a compiler diagnostic and is left to it; an interior constant asks a real question.
/// Floating-point operands are excluded — <c>NaN</c> makes even a self-comparison meaningful — and so is a signed
/// value compared to zero, which is a legitimate test. <c>nuint</c> is handled only on its minimum (zero): its
/// maximum is platform-dependent and not a compile-time constant, while <c>nint</c> is skipped entirely.
/// </para>
/// <para>
/// The clean path stays off the semantic model. A cheap syntactic gate requires exactly one side to be written as
/// a bound — a numeric literal, a negated numeric literal, or a <c>MinValue</c>/<c>MaxValue</c> member access — so
/// an ordinary <c>a &lt; b</c> never binds. Only then is the bound resolved to a constant, the operand's type
/// resolved to a fixed-range integer, and the pair matched against the type's edge.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2489TypeDecidedComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fixed range of every integer type this rule reasons about, keyed by its special type.</summary>
    private static readonly Dictionary<SpecialType, IntegerDomain> Domains = new()
    {
        [SpecialType.System_Byte] = new IntegerDomain("byte", byte.MinValue, byte.MaxValue),
        [SpecialType.System_SByte] = new IntegerDomain("sbyte", sbyte.MinValue, sbyte.MaxValue),
        [SpecialType.System_Int16] = new IntegerDomain("short", short.MinValue, short.MaxValue),
        [SpecialType.System_UInt16] = new IntegerDomain("ushort", ushort.MinValue, ushort.MaxValue),
        [SpecialType.System_Int32] = new IntegerDomain("int", int.MinValue, int.MaxValue),
        [SpecialType.System_UInt32] = new IntegerDomain("uint", uint.MinValue, uint.MaxValue),
        [SpecialType.System_Int64] = new IntegerDomain("long", long.MinValue, long.MaxValue),
        [SpecialType.System_UInt64] = new IntegerDomain("ulong", ulong.MinValue, ulong.MaxValue),

        // The native unsigned integer has a fixed minimum but a platform-dependent maximum, so only its floor is set.
        [SpecialType.System_UIntPtr] = new IntegerDomain("nuint", 0m, null),
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.TypeDecidedComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression);
    }

    /// <summary>Reports one relational comparison the operand's type already decides.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;

        // Exactly one side must be written as a bound; anything else never touches the semantic model.
        var leftIsBound = IsBoundCandidate(comparison.Left);
        var rightIsBound = IsBoundCandidate(comparison.Right);
        if (leftIsBound == rightIsBound)
        {
            return;
        }

        var operand = leftIsBound ? comparison.Right : comparison.Left;
        var bound = leftIsBound ? comparison.Left : comparison.Right;

        // Orient the operator as `operand OP bound`, so `0 <= x` reads as `x >= 0`.
        var kind = leftIsBound ? Flip(comparison.Kind()) : comparison.Kind();

        if (!TryGetIntegralConstant(context, bound, out var constant)
            || !TryGetDomain(context.SemanticModel.GetTypeInfo(operand, context.CancellationToken).Type, out var domain)
            || context.SemanticModel.GetConstantValue(operand, context.CancellationToken).HasValue)
        {
            return;
        }

        if (!TryGetVerdict(kind, constant, domain, out var verdict))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.TypeDecidedComparison,
            comparison.GetLocation(),
            comparison.ToString(),
            verdict));
    }

    /// <summary>Returns whether an operand is written as a range bound, from syntax alone.</summary>
    /// <param name="expression">The operand to inspect.</param>
    /// <returns><see langword="true"/> for a numeric literal, a negated numeric literal, or a <c>MinValue</c>/<c>MaxValue</c> access.</returns>
    private static bool IsBoundCandidate(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => IsNumericLiteral(literal),
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } negated => IsNumericLiteral(negated.Operand),
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText is "MinValue" or "MaxValue",
        _ => false,
    };

    /// <summary>Returns whether an expression is a numeric literal token.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for a numeric literal.</returns>
    private static bool IsNumericLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression };

    /// <summary>Mirrors a comparison so the operand can always be read as the left side.</summary>
    /// <param name="kind">The comparison as written.</param>
    /// <returns>The comparison with its operands swapped.</returns>
    private static SyntaxKind Flip(SyntaxKind kind) => kind switch
    {
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
        _ => SyntaxKind.GreaterThanOrEqualExpression,
    };

    /// <summary>Reads a bound as an integral compile-time constant.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The bound expression.</param>
    /// <param name="value">The constant, widened to <see cref="decimal"/> so every integer type compares exactly.</param>
    /// <returns><see langword="true"/> when the bound is an integral constant.</returns>
    private static bool TryGetIntegralConstant(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, out decimal value)
    {
        value = 0m;
        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        if (constant is not { HasValue: true, Value: { } boxed } || !IsIntegral(boxed))
        {
            return false;
        }

        value = Convert.ToDecimal(boxed, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Returns whether a boxed constant is one of the fixed-width integer types.</summary>
    /// <param name="value">The boxed constant.</param>
    /// <returns><see langword="true"/> for a signed or unsigned integer.</returns>
    private static bool IsIntegral(object value)
        => value is int or long or short or byte or sbyte or ushort or uint or ulong;

    /// <summary>Resolves the fixed range of an integer operand type.</summary>
    /// <param name="type">The operand's type.</param>
    /// <param name="domain">The type's range and keyword.</param>
    /// <returns><see langword="true"/> when the type is a fixed-range integer this rule reasons about.</returns>
    private static bool TryGetDomain(ITypeSymbol? type, out IntegerDomain domain)
    {
        if (type is not null && Domains.TryGetValue(type.SpecialType, out domain))
        {
            return true;
        }

        domain = default;
        return false;
    }

    /// <summary>Decides whether the comparison is fixed by the operand type, and builds the verdict clause.</summary>
    /// <param name="kind">The comparison, oriented as <c>operand OP bound</c>.</param>
    /// <param name="constant">The bound value.</param>
    /// <param name="domain">The operand type's range.</param>
    /// <param name="verdict">The message tail describing why the comparison is decided.</param>
    /// <returns><see langword="true"/> when the constant sits on the type's minimum or maximum edge.</returns>
    private static bool TryGetVerdict(SyntaxKind kind, decimal constant, IntegerDomain domain, out string verdict)
    {
        if (constant == domain.Min)
        {
            return TryGetMinimumVerdict(kind, domain, out verdict);
        }

        if (domain.Max is { } max && constant == max)
        {
            return TryGetMaximumVerdict(kind, domain, out verdict);
        }

        verdict = string.Empty;
        return false;
    }

    /// <summary>Builds the verdict for a comparison against the type's minimum.</summary>
    /// <param name="kind">The comparison, oriented as <c>operand OP minimum</c>.</param>
    /// <param name="domain">The operand type's range.</param>
    /// <param name="verdict">The message tail.</param>
    /// <returns><see langword="true"/> when the operator makes the minimum decide the result.</returns>
    private static bool TryGetMinimumVerdict(SyntaxKind kind, IntegerDomain domain, out string verdict)
    {
        switch (kind)
        {
            case SyntaxKind.GreaterThanOrEqualExpression:
            {
                verdict = AlwaysTrue(domain, atMin: true);
                return true;
            }

            case SyntaxKind.LessThanExpression:
            {
                verdict = AlwaysFalse(domain, atMin: true);
                return true;
            }

            case SyntaxKind.GreaterThanExpression:
            {
                verdict = ReallyNotEqual(domain);
                return true;
            }

            default:
            {
                verdict = string.Empty;
                return false;
            }
        }
    }

    /// <summary>Builds the verdict for a comparison against the type's maximum.</summary>
    /// <param name="kind">The comparison, oriented as <c>operand OP maximum</c>.</param>
    /// <param name="domain">The operand type's range.</param>
    /// <param name="verdict">The message tail.</param>
    /// <returns><see langword="true"/> when the operator makes the maximum decide the result.</returns>
    private static bool TryGetMaximumVerdict(SyntaxKind kind, IntegerDomain domain, out string verdict)
    {
        switch (kind)
        {
            case SyntaxKind.LessThanOrEqualExpression:
            {
                verdict = AlwaysTrue(domain, atMin: false);
                return true;
            }

            case SyntaxKind.GreaterThanExpression:
            {
                verdict = AlwaysFalse(domain, atMin: false);
                return true;
            }

            default:
            {
                verdict = string.Empty;
                return false;
            }
        }
    }

    /// <summary>Builds the tail for a comparison the type makes always true.</summary>
    /// <param name="domain">The operand type's range.</param>
    /// <param name="atMin">Whether the bound is the type's minimum rather than its maximum.</param>
    /// <returns>The message tail.</returns>
    private static string AlwaysTrue(IntegerDomain domain, bool atMin)
        => atMin
            ? $"is always true because a '{domain.Keyword}' value is never {BelowMin(domain)}"
            : $"is always true because a '{domain.Keyword}' value never exceeds its maximum";

    /// <summary>Builds the tail for a comparison the type makes always false.</summary>
    /// <param name="domain">The operand type's range.</param>
    /// <param name="atMin">Whether the bound is the type's minimum rather than its maximum.</param>
    /// <returns>The message tail.</returns>
    private static string AlwaysFalse(IntegerDomain domain, bool atMin)
        => atMin
            ? $"is always false because a '{domain.Keyword}' value is never {BelowMin(domain)}"
            : $"is always false because a '{domain.Keyword}' value never exceeds its maximum";

    /// <summary>Builds the tail for a strict comparison against the minimum, which is an inequality in disguise.</summary>
    /// <param name="domain">The operand type's range.</param>
    /// <returns>The message tail.</returns>
    private static string ReallyNotEqual(IntegerDomain domain)
    {
        var minimum = domain.Min.ToString(CultureInfo.InvariantCulture);
        return $"is only false when the value equals {minimum}, so on a '{domain.Keyword}' it is really a '!= {minimum}' check";
    }

    /// <summary>Names the edge a value can never fall below the minimum.</summary>
    /// <param name="domain">The operand type's range.</param>
    /// <returns><c>negative</c> for an unsigned type, otherwise a phrase naming the minimum.</returns>
    private static string BelowMin(IntegerDomain domain)
        => domain.Min == 0m ? "negative" : "below its minimum";

    /// <summary>The fixed range of an integer type, and its keyword for the diagnostic message.</summary>
    /// <param name="Keyword">The C# keyword naming the type.</param>
    /// <param name="Min">The type's minimum value.</param>
    /// <param name="Max">The type's maximum value, or <see langword="null"/> when it is not a compile-time constant.</param>
    private readonly record struct IntegerDomain(string Keyword, decimal Min, decimal? Max);
}
