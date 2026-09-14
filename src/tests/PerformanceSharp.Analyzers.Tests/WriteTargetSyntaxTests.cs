// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the syntactic write-target classification of element accesses and identifiers.</summary>
public class WriteTargetSyntaxTests
{
    /// <summary>Verifies an element access counts as written only where its storage location is needed.</summary>
    /// <param name="statement">A statement containing one <c>a[0]</c> access.</param>
    /// <param name="expected">Whether the access is a write target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a[0] = 1;", true)]
    [Arguments("a[0] += 1;", true)]
    [Arguments("x = a[0];", false)]
    [Arguments("a[0]++;", true)]
    [Arguments("a[0]--;", true)]
    [Arguments("++a[0];", true)]
    [Arguments("--a[0];", true)]
    [Arguments("x = -a[0];", false)]
    [Arguments("x = ~a[0];", false)]
    [Arguments("ref int r = ref a[0];", true)]
    [Arguments("M(ref a[0]);", true)]
    [Arguments("M(out a[0]);", true)]
    [Arguments("M(in a[0]);", true)]
    [Arguments("M(a[0]);", false)]
    [Arguments("foreach (var c in a[0]) { }", false)]
    public async Task ElementWriteTargetNeedsTheStorageLocationAsync(string statement, bool expected)
    {
        var elementAccess = SyntaxFactory.ParseStatement(statement).DescendantNodes().OfType<ElementAccessExpressionSyntax>().Single();

        await Assert.That(WriteTargetSyntax.IsElementWriteTarget(elementAccess)).IsEqualTo(expected);
    }

    /// <summary>Verifies an identifier counts as written when assigned, passed by reference, or under any unary operator.</summary>
    /// <param name="statement">A statement containing one <c>x</c> identifier.</param>
    /// <param name="expected">Whether the identifier is a write target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("x = 1;", true)]
    [Arguments("x += 1;", true)]
    [Arguments("y = x;", false)]
    [Arguments("x++;", true)]
    [Arguments("--x;", true)]
    [Arguments("y = -x;", true)]
    [Arguments("y = !x;", true)]
    [Arguments("M(ref x);", true)]
    [Arguments("M(out x);", true)]
    [Arguments("M(in x);", true)]
    [Arguments("M(x);", false)]
    [Arguments("y = x.Length;", false)]
    public async Task IdentifierWriteTargetIncludesEveryUnaryOperandAsync(string statement, bool expected)
    {
        var identifier = SyntaxFactory.ParseStatement(statement)
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Single(static name => name.Identifier.ValueText == "x");

        await Assert.That(WriteTargetSyntax.IsIdentifierWriteTarget(identifier)).IsEqualTo(expected);
    }
}
