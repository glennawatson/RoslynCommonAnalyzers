// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Answers the one precedence question several modern-syntax rewrites share: is an expression a primary
/// expression, so it can stand as a member-access receiver or take a leading <c>!</c> without parentheses.
/// </summary>
internal static class PrimaryExpressionClassification
{
    /// <summary>Returns whether an expression is a primary expression that never needs parentheses in a tighter-binding position.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for names, invocations, member and element accesses, literals, and the other primary forms.</returns>
    public static bool IsPrimary(ExpressionSyntax expression)
        => IsPrimaryName(expression) || IsPrimaryTerminal(expression);

    /// <summary>Returns whether an expression is a name, member access, invocation, or similar navigation form.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for the navigation-shaped primary expressions.</returns>
    private static bool IsPrimaryName(ExpressionSyntax expression) => expression is
        IdentifierNameSyntax or GenericNameSyntax or MemberAccessExpressionSyntax
        or ElementAccessExpressionSyntax or InvocationExpressionSyntax or ConditionalAccessExpressionSyntax;

    /// <summary>Returns whether an expression is a self-contained primary value such as a literal or creation.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for the terminal primary expressions.</returns>
    private static bool IsPrimaryTerminal(ExpressionSyntax expression) => expression is
        ThisExpressionSyntax or BaseExpressionSyntax or ParenthesizedExpressionSyntax or LiteralExpressionSyntax
        or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax or TupleExpressionSyntax;
}
