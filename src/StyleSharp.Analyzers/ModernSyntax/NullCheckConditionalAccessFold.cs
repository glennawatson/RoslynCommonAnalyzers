// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Recognizes <c>x != null &amp;&amp; &lt;use of x&gt;</c> conjunctions that a conditional access states in one
/// place, and hands the analyzer and its code fix the same decision so the two cannot drift.
/// </summary>
/// <remarks>
/// Matching is syntax-first — the conjunction's shape, the receiver's repetition, and the operator are all
/// settled before the semantic model is asked anything — so a conjunction that is not a guarded member read
/// costs one pattern match.
/// </remarks>
internal static class NullCheckConditionalAccessFold
{
    /// <summary>The member name a nullable value is unwrapped through.</summary>
    private const string ValueMemberName = "Value";

    /// <summary>Classifies a logical-and expression as a foldable guarded member read.</summary>
    /// <param name="conjunction">The <c>&amp;&amp;</c> expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="receiver">The guarded value, repeated on both sides.</param>
    /// <param name="guardedUse">The right operand of the conjunction.</param>
    /// <returns>The fold shape, or <see cref="NullCheckFoldKind.None"/> when the conjunction does not match.</returns>
    public static NullCheckFoldKind Classify(
        BinaryExpressionSyntax conjunction,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax receiver,
        out ExpressionSyntax guardedUse)
    {
        receiver = null!;
        guardedUse = null!;
        if (conjunction.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp6 })
        {
            return NullCheckFoldKind.None;
        }

        if (TryGetGuardedReceiver(conjunction.Left) is not { } guarded)
        {
            return NullCheckFoldKind.None;
        }

        receiver = guarded;
        guardedUse = conjunction.Right;
        return ClassifyUse(guardedUse, guarded, model, cancellationToken);
    }

    /// <summary>Finds the member access whose own receiver is the guarded value.</summary>
    /// <param name="use">The expression built on the guarded value.</param>
    /// <param name="receiver">The guarded value.</param>
    /// <returns>The member access to turn into a member binding, or <see langword="null"/> when there is none.</returns>
    public static MemberAccessExpressionSyntax? FindRootAccess(ExpressionSyntax use, ExpressionSyntax receiver)
    {
        for (var current = use; current is not null;)
        {
            if (current is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access)
            {
                if (IsSameValue(access.Expression, receiver))
                {
                    return access;
                }

                current = access.Expression;
                continue;
            }

            current = current switch
            {
                InvocationExpressionSyntax invocation => invocation.Expression,
                ElementAccessExpressionSyntax element => element.Expression,
                _ => null,
            };
        }

        return null;
    }

    /// <summary>Returns whether two expressions name the same value.</summary>
    /// <param name="left">The first expression.</param>
    /// <param name="right">The second expression.</param>
    /// <returns><see langword="true"/> when the two are written identically.</returns>
    public static bool IsSameValue(ExpressionSyntax left, ExpressionSyntax right)
        => SyntaxFactory.AreEquivalent(left, right, topLevel: false);

    /// <summary>Classifies what the conjunction does with the guarded value.</summary>
    /// <param name="use">The conjunction's right operand.</param>
    /// <param name="receiver">The guarded value.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The fold shape, or <see cref="NullCheckFoldKind.None"/> when the use does not match.</returns>
    private static NullCheckFoldKind ClassifyUse(
        ExpressionSyntax use,
        ExpressionSyntax receiver,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (IsNullableBooleanValueRead(use, receiver, model, cancellationToken))
        {
            return NullCheckFoldKind.NullableBooleanValue;
        }

        if (use is BinaryExpressionSyntax comparison && IsFoldableComparison(comparison))
        {
            return FindRootAccess(comparison.Left, receiver) is not null && !Mentions(comparison.Right, receiver)
                ? NullCheckFoldKind.Comparison
                : NullCheckFoldKind.None;
        }

        var isBooleanMember = FindRootAccess(use, receiver) is not null
            && model.GetTypeInfo(use, cancellationToken).Type is { SpecialType: SpecialType.System_Boolean };

        return isBooleanMember ? NullCheckFoldKind.BooleanMember : NullCheckFoldKind.None;
    }

    /// <summary>Gets the value a <c>x != null</c> guard protects.</summary>
    /// <param name="guard">The conjunction's left operand.</param>
    /// <returns>The guarded value, or <see langword="null"/> when the operand is not a null guard.</returns>
    private static ExpressionSyntax? TryGetGuardedReceiver(ExpressionSyntax guard)
    {
        if (guard is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression } comparison)
        {
            return null;
        }

        ExpressionSyntax? value = null;
        if (IsNullLiteral(comparison.Right))
        {
            value = comparison.Left;
        }
        else if (IsNullLiteral(comparison.Left))
        {
            value = comparison.Right;
        }

        return value is not null && CompoundAssignmentOperators.IsSideEffectFreeTarget(value) ? value : null;
    }

    /// <summary>Returns whether an expression is the <c>null</c> literal.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for a bare <c>null</c>.</returns>
    private static bool IsNullLiteral(ExpressionSyntax expression)
        => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns whether a comparison keeps its answer when an operand becomes null.</summary>
    /// <param name="comparison">The candidate comparison.</param>
    /// <returns><see langword="true"/> for the relational operators and <c>==</c> against a non-null constant.</returns>
    /// <remarks>
    /// A relational comparison against null is false, and so is <c>== c</c> for a non-null constant <c>c</c>,
    /// which is exactly what the guard produced. <c>!=</c> is excluded because <c>null != c</c> is true.
    /// </remarks>
    private static bool IsFoldableComparison(BinaryExpressionSyntax comparison) => comparison.Kind() switch
    {
        SyntaxKind.GreaterThanExpression => true,
        SyntaxKind.GreaterThanOrEqualExpression => true,
        SyntaxKind.LessThanExpression => true,
        SyntaxKind.LessThanOrEqualExpression => true,
        SyntaxKind.EqualsExpression => !IsNullLiteral(comparison.Right),
        _ => false,
    };

    /// <summary>Returns whether an expression reads <c>.Value</c> off a <c>bool?</c> guarded value.</summary>
    /// <param name="use">The conjunction's right operand.</param>
    /// <param name="receiver">The guarded value.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for <c>b.Value</c> where <c>b</c> is <c>bool?</c>.</returns>
    private static bool IsNullableBooleanValueRead(
        ExpressionSyntax use,
        ExpressionSyntax receiver,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (use is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: ValueMemberName } } access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || !IsSameValue(access.Expression, receiver))
        {
            return false;
        }

        var type = model.GetTypeInfo(receiver, cancellationToken).Type;
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullable
            && nullable.TypeArguments[0].SpecialType == SpecialType.System_Boolean;
    }

    /// <summary>Returns whether an expression mentions the guarded value anywhere.</summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="receiver">The guarded value.</param>
    /// <returns><see langword="true"/> when the guarded value appears in the expression.</returns>
    /// <remarks>
    /// The comparison's right operand is evaluated whether or not the receiver is null once the guard folds
    /// away, so a right operand that reads the guarded value would start dereferencing a null it used to be
    /// protected from.
    /// </remarks>
    private static bool Mentions(ExpressionSyntax expression, ExpressionSyntax receiver)
    {
        if (IsSameValue(expression, receiver))
        {
            return true;
        }

        var state = (Receiver: receiver, Found: false);
        DescendantTraversalHelper.VisitDescendants<ExpressionSyntax, (ExpressionSyntax Receiver, bool Found)>(
            expression,
            ref state,
            static (node, ref current) =>
            {
                if (!IsSameValue(node, current.Receiver))
                {
                    return true;
                }

                current.Found = true;
                return false;
            });

        return state.Found;
    }
}
