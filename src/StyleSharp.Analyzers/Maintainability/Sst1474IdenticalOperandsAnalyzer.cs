// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a binary operator whose two operands are the same expression (SST1474) — <c>x == x</c>,
/// <c>a &amp;&amp; a</c>, <c>value - value</c> — which either answers a constant or does nothing, and is
/// almost always a mistyped operand.
/// </summary>
/// <remarks>
/// <para>
/// <c>+</c> and <c>*</c> are not reported: doubling and squaring are what <c>x + x</c> and <c>x * x</c> are
/// for. Every other operator over identical operands is either a constant (<c>x == x</c>, <c>x - x</c>,
/// <c>x / x</c>, <c>x % x</c>, <c>x ^ x</c>, <c>x &lt; x</c>) or a no-op (<c>x &amp; x</c>, <c>x | x</c>,
/// <c>a &amp;&amp; a</c>), and none of those is worth writing.
/// </para>
/// <para>
/// Only side-effect-free operands are compared, so <c>Next() == Next()</c> stays clean: it calls two
/// different things that happen to be spelled the same way. See <see cref="SideEffectFreeExpression"/> for
/// what that admits.
/// </para>
/// <para>
/// An exact <c>==</c> / <c>!=</c> on a binary floating-point type is left to SST1473. <c>x != x</c> on a
/// <see cref="double"/> is the deliberate NaN idiom, and the fix for it — <c>double.IsNaN(x)</c> — is a
/// floating-point fix, not a copy-paste one. Two rules reporting the same span would say it twice.
/// </para>
/// <para>
/// Ordered so the clean path never binds and rarely walks. Two operands of different syntax kinds cannot be
/// the same expression, so the overwhelmingly common <c>a == b</c> is rejected on a single integer compare;
/// the structural comparison runs only for same-kind operands, and the semantic floating-point check runs
/// only once a structural match has already been found.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1474IdenticalOperandsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.IdenticalOperands);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.BitwiseAndExpression,
            SyntaxKind.BitwiseOrExpression,
            SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.SubtractExpression,
            SyntaxKind.DivideExpression,
            SyntaxKind.ModuloExpression);
    }

    /// <summary>Reports one operator whose operands are the same, side-effect-free expression.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var left = binary.Left;
        var right = binary.Right;

        // The kind check is the prepass: two nodes of different kinds are never equivalent, and it rejects
        // nearly every operator in a file before the structural walk is entered. The purity test then runs
        // before the walk because it is the cheaper of the two, and it is what keeps 'Next() == Next()' out.
        if (left.RawKind != right.RawKind
            || !SideEffectFreeExpression.IsSideEffectFree(left)
            || !SyntaxFactory.AreEquivalent(left, right, topLevel: false))
        {
            return;
        }

        if (IsFloatingPointEquality(context, binary))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.IdenticalOperands,
            binary.SyntaxTree,
            binary.Span,
            binary.OperatorToken.Text));
    }

    /// <summary>Returns whether the comparison is the floating-point NaN idiom that SST1473 owns.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="binary">The comparison whose operands already match.</param>
    /// <returns><see langword="true"/> when an <c>==</c> or <c>!=</c> compares binary floating-point values.</returns>
    /// <remarks>
    /// This is the only place the rule binds, and it is reached only by an operator whose operands have
    /// already been proved identical — so a clean file never pays for it. The operands are the same
    /// expression, so the left one settles the type.
    /// </remarks>
    private static bool IsFloatingPointEquality(SyntaxNodeAnalysisContext context, BinaryExpressionSyntax binary)
    {
        if (binary.RawKind is not ((int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression))
        {
            return false;
        }

        var type = context.SemanticModel.GetTypeInfo(binary.Left, context.CancellationToken).ConvertedType;
        return FloatingPointTypes.IsBinaryFloatingPoint(type);
    }
}
