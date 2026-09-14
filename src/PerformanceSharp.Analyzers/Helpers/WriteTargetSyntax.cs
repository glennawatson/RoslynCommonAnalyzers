// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Syntactic tests for whether a reference is written rather than only read, shared by the rules that may
/// only rewrite a read: the element-access form (PSH1217, PSH1226) and the identifier form (PSH1304,
/// PSH1421). Neither binds anything.
/// </summary>
internal static class WriteTargetSyntax
{
    /// <summary>Returns whether an element access writes to, or takes a reference to, its element.</summary>
    /// <param name="elementAccess">The element access.</param>
    /// <returns><see langword="true"/> for an assignment target, an increment or decrement, a <c>ref</c> expression, or a <c>ref</c>/<c>out</c>/<c>in</c> argument.</returns>
    /// <remarks>
    /// An array element is a writable storage location; a <c>string</c> or <c>ReadOnlySpan&lt;T&gt;</c> element
    /// is not. Every shape that needs the location rather than the value stops compiling once the array is
    /// replaced by one of those.
    /// </remarks>
    internal static bool IsElementWriteTarget(ElementAccessExpressionSyntax elementAccess) => elementAccess.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == elementAccess,
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression }
            or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression }
            or RefExpressionSyntax => true,
        ArgumentSyntax argument => argument.RefOrOutKeyword.RawKind != (int)SyntaxKind.None,
        _ => false,
    };

    /// <summary>Returns whether an identifier may be written by its immediate parent.</summary>
    /// <param name="identifier">The identifier occurrence.</param>
    /// <returns><see langword="true"/> for an assignment target, the operand of any prefix or postfix operator, or a <c>ref</c>/<c>out</c>/<c>in</c> argument.</returns>
    /// <remarks>
    /// Every prefix and postfix operator counts, not only increments and decrements, so a negation or a logical
    /// not over the name is treated as a write too. The over-approximation only ever keeps a rule quiet.
    /// </remarks>
    internal static bool IsIdentifierWriteTarget(IdentifierNameSyntax identifier) =>
        identifier.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == identifier,
            PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
            ArgumentSyntax argument => !argument.RefOrOutKeyword.IsKind(SyntaxKind.None),
            _ => false,
        };
}
