// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Recognises the empty string written as the literal <c>""</c> or as <see cref="string.Empty"/>. The
/// syntactic checks run first and only a <c>.Empty</c> access needs the semantic model to confirm it binds
/// to the <see cref="string"/> field rather than some other type's <c>Empty</c>.
/// </summary>
internal static class EmptyStringExpressions
{
    /// <summary>Returns whether an expression is the literal <c>""</c>.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for a string literal whose value is empty.</returns>
    internal static bool IsEmptyStringLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && literal.Token.ValueText.Length == 0;

    /// <summary>Returns whether an expression is a simple member access named <c>Empty</c>, syntactically.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for <c>X.Empty</c>; whether it is <see cref="string.Empty"/> needs <see cref="IsStringEmptyField"/>.</returns>
    internal static bool IsEmptyMemberAccess(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Name is IdentifierNameSyntax { Identifier.ValueText: "Empty" };

    /// <summary>Returns whether an expression binds to the <see cref="string.Empty"/> field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The expression to bind, normally one <see cref="IsEmptyMemberAccess"/> accepted.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression is the static <c>Empty</c> field on <see cref="string"/>.</returns>
    internal static bool IsStringEmptyField(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken) =>
        model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol
        {
            IsStatic: true,
            ContainingType.SpecialType: SpecialType.System_String
        };
}
