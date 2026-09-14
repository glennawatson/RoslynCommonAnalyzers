// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared mirroring of relational comparison kinds.</summary>
public sealed class ComparisonKindsUnitTest
{
    /// <summary>Verifies each relational comparison maps to the comparison that means the same with its operands swapped.</summary>
    /// <param name="kind">The comparison as written.</param>
    /// <param name="expected">The comparison with its operands swapped.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(SyntaxKind.LessThanExpression, SyntaxKind.GreaterThanExpression)]
    [Arguments(SyntaxKind.LessThanOrEqualExpression, SyntaxKind.GreaterThanOrEqualExpression)]
    [Arguments(SyntaxKind.GreaterThanExpression, SyntaxKind.LessThanExpression)]
    [Arguments(SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.LessThanOrEqualExpression)]
    public async Task MirrorSwapsRelationalComparisonsAsync(SyntaxKind kind, SyntaxKind expected) =>
        await Assert.That(ComparisonKinds.Mirror(kind)).IsEqualTo(expected);

    /// <summary>Verifies symmetric comparisons and non-comparisons come back unchanged.</summary>
    /// <param name="kind">The kind to mirror.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(SyntaxKind.EqualsExpression)]
    [Arguments(SyntaxKind.NotEqualsExpression)]
    [Arguments(SyntaxKind.AddExpression)]
    public async Task MirrorLeavesOtherKindsUnchangedAsync(SyntaxKind kind) =>
        await Assert.That(ComparisonKinds.Mirror(kind)).IsEqualTo(kind);
}
