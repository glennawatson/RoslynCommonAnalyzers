// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the walk to the body of the innermost enclosing function.</summary>
public class EnclosingFunctionTests
{
    /// <summary>Verifies the innermost function's block or expression body is returned, and nothing outside a function.</summary>
    /// <param name="members">Class members containing one <c>Target()</c> call.</param>
    /// <param name="expectedBody">The syntax kind of the expected body, or <see cref="SyntaxKind.None"/> when there is none.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("void M() { Target(); }", SyntaxKind.Block)]
    [Arguments("int M() => Target();", SyntaxKind.ArrowExpressionClause)]
    [Arguments("C() { Target(); }", SyntaxKind.Block)]
    [Arguments("public static C operator +(C a, C b) => Target();", SyntaxKind.ArrowExpressionClause)]
    [Arguments("int P { get { return Target(); } }", SyntaxKind.Block)]
    [Arguments("int P { get => Target(); }", SyntaxKind.ArrowExpressionClause)]
    [Arguments("void M() { System.Func<int> f = () => Target(); }", SyntaxKind.InvocationExpression)]
    [Arguments("void M() { System.Action f = () => { Target(); }; }", SyntaxKind.Block)]
    [Arguments("void M() { int L() => Target(); }", SyntaxKind.ArrowExpressionClause)]
    [Arguments("void M() { void L() { Target(); } }", SyntaxKind.Block)]
    [Arguments("int P => Target();", SyntaxKind.None)]
    [Arguments("int F = Target();", SyntaxKind.None)]
    public async Task BodyBelongsToTheInnermostFunctionAsync(string members, SyntaxKind expectedBody)
    {
        var target = FindTarget($"class C {{ {members} }}");

        await Assert.That(EnclosingFunction.GetBody(target)?.Kind() ?? SyntaxKind.None).IsEqualTo(expectedBody);
    }

    /// <summary>Verifies a node with no function above it, attached or detached, has no body.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NodeOutsideAnyFunctionHasNoBodyAsync()
    {
        await Assert.That(EnclosingFunction.GetBody(SyntaxFactory.ParseExpression("Target()"))).IsNull();
        await Assert.That(EnclosingFunction.GetBody(FindTarget("[assembly: A(Target())]"))).IsNull();
    }

    /// <summary>Finds the single <c>Target()</c> invocation.</summary>
    /// <param name="source">The compilation unit text.</param>
    /// <returns>The invocation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static InvocationExpressionSyntax FindTarget(string source) =>
        SyntaxFactory.ParseCompilationUnit(source)
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(static invocation => invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "Target" });
}
