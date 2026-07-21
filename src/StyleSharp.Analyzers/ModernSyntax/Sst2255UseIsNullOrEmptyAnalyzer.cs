// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a hand-written null-or-empty string test that <c>string.IsNullOrEmpty</c> already names (SST2255):
/// <c>s == null || s.Length == 0</c>, <c>s == null || s == ""</c>, and the negated
/// <c>s != null &amp;&amp; s.Length != 0</c> / <c>s != null &amp;&amp; s != ""</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2255UseIsNullOrEmptyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Which half of the disjunction (or conjunction) an operand is.</summary>
    private enum Part
    {
        /// <summary>The operand is neither a null check nor an emptiness check.</summary>
        None,

        /// <summary>The operand compares the value against <see langword="null"/>.</summary>
        Null,

        /// <summary>The operand checks the value for emptiness.</summary>
        Empty,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseIsNullOrEmpty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalOrExpression);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LogicalAndExpression);
    }

    /// <summary>Matches the disjunction/conjunction and returns the tested value and whether it is the negated form.</summary>
    /// <param name="binary">The <c>||</c> or <c>&amp;&amp;</c> expression.</param>
    /// <param name="value">The side-effect-free value being tested for null-or-empty.</param>
    /// <param name="negated">Whether the shape is the negated <c>!= null &amp;&amp; …</c> form.</param>
    /// <returns><see langword="true"/> when the shape is a null-or-empty test over a single value.</returns>
    internal static bool TryMatch(BinaryExpressionSyntax binary, out ExpressionSyntax value, out bool negated)
    {
        value = null!;
        negated = binary.IsKind(SyntaxKind.LogicalAndExpression);

        var left = Unparenthesize(binary.Left);
        var right = Unparenthesize(binary.Right);
        var leftPart = Classify(left, negated, out var leftValue, out var leftDeref);
        var rightPart = Classify(right, negated, out var rightValue, out var rightDeref);

        ExpressionSyntax nullValue;
        ExpressionSyntax emptyValue;
        bool emptyDeref;
        bool emptyOnLeft;
        if (leftPart == Part.Null && rightPart == Part.Empty)
        {
            (nullValue, emptyValue, emptyDeref, emptyOnLeft) = (leftValue, rightValue, rightDeref, false);
        }
        else if (leftPart == Part.Empty && rightPart == Part.Null)
        {
            (nullValue, emptyValue, emptyDeref, emptyOnLeft) = (rightValue, leftValue, leftDeref, true);
        }
        else
        {
            return false;
        }

        // A '.Length' read before the null check would throw on null, so the fold to a null-safe helper
        // would change behaviour. That order is only safe when the emptiness check does not dereference.
        if (emptyDeref && emptyOnLeft)
        {
            return false;
        }

        if (!SyntaxFactory.AreEquivalent(nullValue, emptyValue) || !SideEffectFreeExpression.IsSideEffectFree(nullValue))
        {
            return false;
        }

        value = nullValue;
        return true;
    }

    /// <summary>Reports the disjunction/conjunction when it tests a string value for null-or-empty.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryMatch(binary, out var value, out _))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(value, context.CancellationToken).Type?.SpecialType != SpecialType.System_String)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseIsNullOrEmpty, binary.GetLocation()));
    }

    /// <summary>Classifies one operand as a null check, an emptiness check, or neither.</summary>
    /// <param name="operand">The unparenthesized operand.</param>
    /// <param name="negated">Whether the enclosing shape is the negated <c>&amp;&amp;</c> form.</param>
    /// <param name="value">The value the operand tests.</param>
    /// <param name="deref">Whether an emptiness check reads <c>.Length</c> (and so dereferences the value).</param>
    /// <returns>The operand's role in a null-or-empty test.</returns>
    private static Part Classify(ExpressionSyntax operand, bool negated, out ExpressionSyntax value, out bool deref)
    {
        value = null!;
        deref = false;
        if (operand is not BinaryExpressionSyntax comparison)
        {
            return Part.None;
        }

        var expected = negated ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression;
        if (!comparison.IsKind(expected))
        {
            return Part.None;
        }

        var left = Unparenthesize(comparison.Left);
        var right = Unparenthesize(comparison.Right);
        if (TryTakeOther(left, right, IsNullLiteral, out var nullOther))
        {
            value = nullOther;
            return Part.Null;
        }

        if (TryTakeOther(left, right, IsEmptyStringLiteral, out var emptyOther))
        {
            value = emptyOther;
            return Part.Empty;
        }

        if (TryTakeLengthAgainstZero(left, right, out var lengthReceiver))
        {
            value = lengthReceiver;
            deref = true;
            return Part.Empty;
        }

        return Part.None;
    }

    /// <summary>Returns the operand opposite a marker side of a comparison.</summary>
    /// <param name="left">The comparison's left operand.</param>
    /// <param name="right">The comparison's right operand.</param>
    /// <param name="isMarker">Recognizes the marker literal (<see langword="null"/> or <c>""</c>).</param>
    /// <param name="other">The value opposite the marker.</param>
    /// <returns><see langword="true"/> when exactly one side is the marker.</returns>
    private static bool TryTakeOther(ExpressionSyntax left, ExpressionSyntax right, Func<ExpressionSyntax, bool> isMarker, out ExpressionSyntax other)
    {
        if (isMarker(right) && !isMarker(left))
        {
            other = left;
            return true;
        }

        if (isMarker(left) && !isMarker(right))
        {
            other = right;
            return true;
        }

        other = null!;
        return false;
    }

    /// <summary>Returns the receiver of a <c>value.Length</c> compared against the literal <c>0</c>.</summary>
    /// <param name="left">The comparison's left operand.</param>
    /// <param name="right">The comparison's right operand.</param>
    /// <param name="receiver">The receiver whose <c>Length</c> is read.</param>
    /// <returns><see langword="true"/> when one side is <c>value.Length</c> and the other is <c>0</c>.</returns>
    private static bool TryTakeLengthAgainstZero(ExpressionSyntax left, ExpressionSyntax right, out ExpressionSyntax receiver)
    {
        if (IsZeroLiteral(right) && IsLengthAccess(left, out receiver))
        {
            return true;
        }

        if (IsZeroLiteral(left) && IsLengthAccess(right, out receiver))
        {
            return true;
        }

        receiver = null!;
        return false;
    }

    /// <summary>Returns whether an expression is a <c>.Length</c> member access.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="receiver">The receiver whose <c>Length</c> is read.</param>
    /// <returns><see langword="true"/> when the expression reads <c>Length</c>.</returns>
    private static bool IsLengthAccess(ExpressionSyntax expression, out ExpressionSyntax receiver)
    {
        if (expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } member)
        {
            receiver = member.Expression;
            return true;
        }

        receiver = null!;
        return false;
    }

    /// <summary>Returns whether an expression is the <see langword="null"/> literal.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for the null literal.</returns>
    private static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns whether an expression is an empty string literal.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for <c>""</c>.</returns>
    private static bool IsEmptyStringLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { Token.ValueText: "" } literal && literal.IsKind(SyntaxKind.StringLiteralExpression);

    /// <summary>Returns whether an expression is the integer literal <c>0</c>.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for <c>0</c>.</returns>
    private static bool IsZeroLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { Token.Value: 0 } literal && literal.IsKind(SyntaxKind.NumericLiteralExpression);

    /// <summary>Strips redundant parentheses from an operand.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns>The operand with any surrounding parentheses removed.</returns>
    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
