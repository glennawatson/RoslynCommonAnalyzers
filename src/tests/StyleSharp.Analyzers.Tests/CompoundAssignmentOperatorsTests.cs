// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests compound operator mappings and targets that can be evaluated once.</summary>
public class CompoundAssignmentOperatorsTests
{
    /// <summary>Checks each supported binary operator maps to the matching assignment syntax.</summary>
    /// <param name="binary">The binary expression kind.</param>
    /// <param name="assignment">The expected assignment kind.</param>
    /// <param name="token">The expected operator token.</param>
    /// <param name="text">The expected diagnostic text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression, SyntaxKind.PlusEqualsToken, "+=")]
    [Arguments(SyntaxKind.SubtractExpression, SyntaxKind.SubtractAssignmentExpression, SyntaxKind.MinusEqualsToken, "-=")]
    [Arguments(SyntaxKind.MultiplyExpression, SyntaxKind.MultiplyAssignmentExpression, SyntaxKind.AsteriskEqualsToken, "*=")]
    [Arguments(SyntaxKind.DivideExpression, SyntaxKind.DivideAssignmentExpression, SyntaxKind.SlashEqualsToken, "/=")]
    [Arguments(SyntaxKind.ModuloExpression, SyntaxKind.ModuloAssignmentExpression, SyntaxKind.PercentEqualsToken, "%=")]
    [Arguments(SyntaxKind.BitwiseAndExpression, SyntaxKind.AndAssignmentExpression, SyntaxKind.AmpersandEqualsToken, "&=")]
    [Arguments(SyntaxKind.BitwiseOrExpression, SyntaxKind.OrAssignmentExpression, SyntaxKind.BarEqualsToken, "|=")]
    [Arguments(SyntaxKind.ExclusiveOrExpression, SyntaxKind.ExclusiveOrAssignmentExpression, SyntaxKind.CaretEqualsToken, "^=")]
    [Arguments(SyntaxKind.LeftShiftExpression, SyntaxKind.LeftShiftAssignmentExpression, SyntaxKind.LessThanLessThanEqualsToken, "<<=")]
    [Arguments(SyntaxKind.RightShiftExpression, SyntaxKind.RightShiftAssignmentExpression, SyntaxKind.GreaterThanGreaterThanEqualsToken, ">>=")]
    public async Task BinaryOperatorMapsToCompoundAssignmentAsync(SyntaxKind binary, SyntaxKind assignment, SyntaxKind token, string text)
    {
        var mapped = CompoundAssignmentOperators.TryMap(binary, out var actualAssignment, out var actualToken, out var actualText);

        await Assert.That(mapped).IsTrue();
        await Assert.That(actualAssignment).IsEqualTo(assignment);
        await Assert.That(actualToken).IsEqualTo(token);
        await Assert.That(actualText).IsEqualTo(text);
    }

    /// <summary>Checks unsupported expressions clear every output instead of suggesting an operator.</summary>
    /// <param name="binary">The unsupported expression kind.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(SyntaxKind.EqualsExpression)]
    [Arguments(SyntaxKind.LogicalAndExpression)]
    [Arguments(SyntaxKind.CoalesceExpression)]
    [Arguments(SyntaxKind.UnsignedRightShiftExpression)]
    public async Task UnsupportedOperatorHasNoMappingAsync(SyntaxKind binary)
    {
        var mapped = CompoundAssignmentOperators.TryMap(binary, out var assignment, out var token, out var text);

        await Assert.That(mapped).IsFalse();
        await Assert.That(assignment).IsEqualTo(SyntaxKind.None);
        await Assert.That(token).IsEqualTo(SyntaxKind.None);
        await Assert.That(text).IsEqualTo(string.Empty);
    }

    /// <summary>Checks member chains inherit the safety of their receiver.</summary>
    /// <param name="source">The assignment target.</param>
    /// <param name="expected">Whether the target can be folded.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value", true)]
    [Arguments("this", true)]
    [Arguments("this.child.value", true)]
    [Arguments("owner.child.value", true)]
    [Arguments("GetOwner().value", false)]
    [Arguments("items[index]", false)]
    [Arguments("(owner).value", false)]
    public async Task TargetSafetyFollowsItsReceiverAsync(string source, bool expected)
    {
        var target = SyntaxFactory.ParseExpression(source);

        await Assert.That(CompoundAssignmentOperators.IsSideEffectFreeTarget(target)).IsEqualTo(expected);
    }
}
