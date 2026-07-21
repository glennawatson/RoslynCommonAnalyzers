// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Guards speculative binding against the conditional-access footgun. A member or element binding
/// (<c>?.M</c>, <c>?[i]</c>) is only bindable while it sits inside its enclosing <c>?.</c> chain; once a
/// call reached through that chain is detached — as every <c>ReplaceNode</c>-then-<c>GetSpeculative*</c>
/// rewrite detaches it — the binding is orphaned, and Roslyn's binder dereferences null hunting for the
/// conditional access that is no longer in the tree
/// (<c>FindConditionalAccessNodeForBinding</c> → <c>GetReceiverForConditionalBinding</c>).
/// </summary>
internal static class ConditionalAccessSpeculation
{
    /// <summary>
    /// Determines whether an expression is reached through a conditional access (<c>?.</c> or <c>?[]</c>), so a
    /// detached copy of it — or of a call built around it — would carry an orphaned member or element binding
    /// that cannot be speculatively bound.
    /// </summary>
    /// <param name="expression">
    /// The expression to inspect — typically an invocation's <see cref="InvocationExpressionSyntax.Expression"/>,
    /// i.e. the invoked name and its receiver spine.
    /// </param>
    /// <returns><see langword="true"/> when a member or element binding sits on the receiver spine.</returns>
    /// <remarks>
    /// The walk follows only the receiver spine — member/element access, nested invocation, null-forgiving
    /// <c>!</c>, and parentheses — because a binding anywhere on that spine (<c>receiver?.Prop.Method(...)</c>,
    /// <c>receiver?[i].Method(...)</c>, <c>receiver?.Prop!.Method(...)</c>) is what orphans once the node is
    /// detached. A binding buried inside an argument has its own conditional access carried along and does not
    /// orphan, so arguments are intentionally not followed.
    /// </remarks>
    internal static bool ReachedThroughConditionalAccess(ExpressionSyntax? expression)
    {
        for (var node = expression; node is not null;)
        {
            if (node is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax)
            {
                return true;
            }

            node = node switch
            {
                MemberAccessExpressionSyntax member => member.Expression,
                ElementAccessExpressionSyntax element => element.Expression,
                InvocationExpressionSyntax invocation => invocation.Expression,
                PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppress => suppress.Operand,
                ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
                _ => null,
            };
        }

        return false;
    }
}
