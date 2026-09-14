// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests whether an expression is everything a member yields.</summary>
public class MemberResultTests
{
    /// <summary>Verifies an expression body or a returned value counts through parentheses, and a partial value does not.</summary>
    /// <param name="member">The member declaration; its first invocation is placed.</param>
    /// <param name="expected">Whether the invocation is the whole result.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int M() => Compute();", true)]
    [Arguments("int M() => ((Compute()));", true)]
    [Arguments("int M() { return Compute(); }", true)]
    [Arguments("int M() => Compute() + 1;", false)]
    [Arguments("int M() { var value = Compute(); return value; }", false)]
    [Arguments("void M() { Compute(); }", false)]
    public async Task WholeResultIsTheBodyOrTheReturnedValueAsync(string member, bool expected)
    {
        var invocation = SyntaxFactory.ParseMemberDeclaration(member)!.DescendantNodes().OfType<InvocationExpressionSyntax>().First();

        await Assert.That(MemberResult.IsWholeResult(invocation)).IsEqualTo(expected);
    }
}
