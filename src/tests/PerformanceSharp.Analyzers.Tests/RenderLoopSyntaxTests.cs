// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the ancestor walks that place a node in a render method's loop.</summary>
public class RenderLoopSyntaxTests
{
    /// <summary>Verifies the loop is found only when no nested function or member boundary is crossed first.</summary>
    /// <param name="body">A method body containing one <c>Target()</c> call.</param>
    /// <param name="expectedLoop">The syntax kind of the expected loop, or <see cref="SyntaxKind.None"/> when none is found.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("for (;;) { Target(); }", SyntaxKind.ForStatement)]
    [Arguments("foreach (var x in xs) { Target(); }", SyntaxKind.ForEachStatement)]
    [Arguments("foreach (var (a, b) in xs) { Target(); }", SyntaxKind.ForEachVariableStatement)]
    [Arguments("while (true) { for (;;) { if (x) { Target(); } } }", SyntaxKind.ForStatement)]
    [Arguments("for (;;) { System.Action a = () => Target(); }", SyntaxKind.None)]
    [Arguments("for (;;) { System.Action a = delegate { Target(); }; }", SyntaxKind.None)]
    [Arguments("for (;;) { void Local() { Target(); } }", SyntaxKind.None)]
    [Arguments("while (true) { Target(); }", SyntaxKind.None)]
    [Arguments("Target();", SyntaxKind.None)]
    public async Task EnclosingLoopStopsAtFunctionsAndMembersAsync(string body, SyntaxKind expectedLoop)
    {
        var target = FindTarget($"class C {{ void M() {{ {body} }} }}");

        await Assert.That(RenderLoopSyntax.FindEnclosingLoop(target)?.Kind() ?? SyntaxKind.None).IsEqualTo(expectedLoop);
    }

    /// <summary>Verifies the containing method is found through nested functions and not past a type declaration.</summary>
    /// <param name="source">A compilation unit containing one <c>Target()</c> call.</param>
    /// <param name="expectedMethod">The expected method name, or an empty string when none is found.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { Target(); } }", "M")]
    [Arguments("class C { void M() { System.Action a = () => Target(); } }", "M")]
    [Arguments("class C { void M() { void Local() { Target(); } } }", "M")]
    [Arguments("class C { int P { get { Target(); return 0; } } }", "")]
    [Arguments("class C { void M() { } class D { int P => Target(); } }", "")]
    public async Task ContainingMethodStopsAtTheTypeAsync(string source, string expectedMethod)
    {
        var target = FindTarget(source);

        await Assert.That(RenderLoopSyntax.FindContainingMethod(target)?.Identifier.ValueText ?? string.Empty).IsEqualTo(expectedMethod);
    }

    /// <summary>Verifies a node outside any tree has neither a loop nor a method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedNodeHasNoLoopOrMethodAsync()
    {
        var target = SyntaxFactory.ParseExpression("Target()");

        await Assert.That(RenderLoopSyntax.FindEnclosingLoop(target)).IsNull();
        await Assert.That(RenderLoopSyntax.FindContainingMethod(target)).IsNull();
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
