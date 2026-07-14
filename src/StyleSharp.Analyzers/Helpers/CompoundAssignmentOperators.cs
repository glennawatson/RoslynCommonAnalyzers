// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Maps a binary operator to its compound-assignment counterpart so <c>x = x op y</c> can be rewritten
/// as <c>x op= y</c>. The lookup is a flat switch (a jump table) to keep the rewrite path allocation-free.
/// </summary>
internal static class CompoundAssignmentOperators
{
    /// <summary>Maps a binary expression kind to the matching compound assignment kind, token, and text.</summary>
    /// <param name="binaryKind">The right-hand-side binary expression kind.</param>
    /// <param name="assignmentKind">The matching compound assignment expression kind.</param>
    /// <param name="operatorToken">The matching compound assignment operator token kind.</param>
    /// <param name="text">The compound operator text (for the diagnostic message).</param>
    /// <returns><see langword="true"/> when the binary kind has a compound-assignment form.</returns>
    [SuppressMessage("Critical Code Smell", "S1541:Methods and properties should not be too complex", Justification = "A flat operator-kind switch is a zero-allocation jump table.")]
    public static bool TryMap(SyntaxKind binaryKind, out SyntaxKind assignmentKind, out SyntaxKind operatorToken, out string text)
    {
        (assignmentKind, operatorToken, text) = binaryKind switch
        {
            SyntaxKind.AddExpression => (SyntaxKind.AddAssignmentExpression, SyntaxKind.PlusEqualsToken, "+="),
            SyntaxKind.SubtractExpression => (SyntaxKind.SubtractAssignmentExpression, SyntaxKind.MinusEqualsToken, "-="),
            SyntaxKind.MultiplyExpression => (SyntaxKind.MultiplyAssignmentExpression, SyntaxKind.AsteriskEqualsToken, "*="),
            SyntaxKind.DivideExpression => (SyntaxKind.DivideAssignmentExpression, SyntaxKind.SlashEqualsToken, "/="),
            SyntaxKind.ModuloExpression => (SyntaxKind.ModuloAssignmentExpression, SyntaxKind.PercentEqualsToken, "%="),
            SyntaxKind.BitwiseAndExpression => (SyntaxKind.AndAssignmentExpression, SyntaxKind.AmpersandEqualsToken, "&="),
            SyntaxKind.BitwiseOrExpression => (SyntaxKind.OrAssignmentExpression, SyntaxKind.BarEqualsToken, "|="),
            SyntaxKind.ExclusiveOrExpression => (SyntaxKind.ExclusiveOrAssignmentExpression, SyntaxKind.CaretEqualsToken, "^="),
            SyntaxKind.LeftShiftExpression => (SyntaxKind.LeftShiftAssignmentExpression, SyntaxKind.LessThanLessThanEqualsToken, "<<="),
            SyntaxKind.RightShiftExpression => (SyntaxKind.RightShiftAssignmentExpression, SyntaxKind.GreaterThanGreaterThanEqualsToken, ">>="),
            _ => (SyntaxKind.None, SyntaxKind.None, string.Empty)
        };
        return operatorToken != SyntaxKind.None;
    }

    /// <summary>Returns whether an assignment target is side-effect-free enough to fold into a compound form.</summary>
    /// <param name="target">The left-hand side of the assignment.</param>
    /// <returns><see langword="true"/> for an identifier, <c>this</c>, or a member-access chain of those.</returns>
    public static bool IsSideEffectFreeTarget(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax memberAccess => IsSideEffectFreeTarget(memberAccess.Expression),
        _ => false
    };
}
