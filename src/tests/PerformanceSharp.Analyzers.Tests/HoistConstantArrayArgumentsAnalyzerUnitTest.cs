// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1004HoistConstantArrayArgumentsAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests constant array arguments and the syntax prefilter used by PSH1004.</summary>
public class HoistConstantArrayArgumentsAnalyzerUnitTest
{
    /// <summary>Verifies literal, signed, named, and qualified constants are hoistable.</summary>
    /// <param name="creation">The constant array argument.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("new int[] { 1, 2 }")]
    [Arguments("new[] { -1, +2, ~0 }")]
    [Arguments("new[] { Value, int.MaxValue }")]
    [Arguments("new string[] { null, \"value\" }")]
    [Arguments("new[] { Day.Monday, Day.Tuesday }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConstantArgumentIsReportedAsync(string creation) =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                private const int Value = 3;
                public enum Day { Monday, Tuesday }
                public void M() => Use({|PSH1004:{{creation}}|});
                private static void Use<T>(T[] values) { }
            }
            """);

    /// <summary>Verifies both explicit and target-typed constructor arguments are reported.</summary>
    /// <param name="call">The constructor call containing the marked array.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("new C({|PSH1004:new[] { 1 }|})")]
    [Arguments("new({|PSH1004:new int[] { 1 }|})")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConstructorArgumentIsReportedAsync(string call) =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public C(int[] values) { }
                public static C Create() => {{call}};
            }
            """);

    /// <summary>Verifies absent, empty, computed, and runtime initializers remain unchanged.</summary>
    /// <param name="creation">The array that cannot be hoisted by the current rule.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("new int[1]")]
    [Arguments("new int[] { }")]
    [Arguments("new[] { 1, value }")]
    [Arguments("new[] { 1, C.RuntimeValue }")]
    [Arguments("new[] { 1, GetValue() }")]
    [Arguments("new[] { -value }")]
    [Arguments("new[] { 1 + 2 }")]
    [Arguments("new[] { (1) }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonCandidateArgumentIsCleanAsync(string creation) =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public static int RuntimeValue => 1;
                public void M(int value) => Use({{creation}});
                private static int GetValue() => 1;
                private static void Use(int[] values) { }
            }
            """);

    /// <summary>Verifies arrays outside direct invocation arguments are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonCallArrayPositionsAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            [Values(new[] { 1 })]
            public class C
            {
                public int[] Field = new[] { 1 };
                public int[] M() => new[] { 1 };
                public int this[int[] values] => 1;
                public void Use()
                {
                    var local = new[] { 1 };
                    _ = this[new[] { 1 }];
                    Take((new[] { 1 }));
                }

                private static void Take(int[] values) { }
            }
            public sealed class ValuesAttribute : Attribute
            {
                public ValuesAttribute(int[] values) { }
            }
            """);

    /// <summary>Verifies a constant array inside a field's call still qualifies.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CallInFieldInitializerIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Field = Take({|PSH1004:new[] { 1 }|});
                private static int Take(int[] values) => values.Length;
            }
            """);

    /// <summary>Verifies unresolved array elements cannot be hoisted while code is being edited.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedArrayElementIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M() => Take(new int[] { {|CS0103:Missing|} });
                private static void Take(int[] values) { }
            }
            """);

    /// <summary>Verifies a call in a statement stops ancestor inspection at that statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CallInStatementIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    Take({|PSH1004:new[] { 1 }|});
                }

                private static void Take(int[] values) { }
            }
            """);

    /// <summary>Verifies unsupported creation syntax has no candidate initializer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonArrayExpressionHasNoInitializerAsync()
    {
        var expression = SyntaxFactory.ParseExpression("new C()");

        await Assert.That(Psh1004HoistConstantArrayArgumentsAnalyzer.TryGetCandidateInitializer(expression, out var initializer)).IsFalse();
        await Assert.That(initializer).IsNull();
    }

    /// <summary>Verifies an argument detached from its call has no candidate initializer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DetachedArgumentHasNoInitializerAsync()
    {
        var argument = SyntaxFactory.Argument(SyntaxFactory.ParseExpression("new[] { 1 }"));

        await Assert.That(Psh1004HoistConstantArrayArgumentsAnalyzer.TryGetCandidateInitializer(argument.Expression, out var initializer)).IsFalse();
        await Assert.That(initializer).IsNull();
    }

    /// <summary>Verifies incomplete parent chains and non-call argument lists are rejected.</summary>
    /// <param name="source">The syntax fragment surrounding the array.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("(new[] { 1 }, 2)")]
    [Arguments("class C : B { C() : base(new[] { 1 }) { } }")]
    [Arguments("[A(F(new[] { 1 }))] class C { }")]
    public async Task UnsupportedArrayParentIsRejectedAsync(string source)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        var creation = root.DescendantNodes().OfType<ImplicitArrayCreationExpressionSyntax>().Single();

        await Assert.That(Psh1004HoistConstantArrayArgumentsAnalyzer.TryGetCandidateInitializer(creation, out var initializer)).IsFalse();
        await Assert.That(initializer).IsNull();
    }

    /// <summary>Verifies the candidate check works for a detached invocation with no member ancestor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DetachedCallRetainsItsInitializerAsync()
    {
        var expression = SyntaxFactory.ParseExpression("Take(new[] { 1 })");
        var creation = expression.DescendantNodes().OfType<ImplicitArrayCreationExpressionSyntax>().Single();

        await Assert.That(Psh1004HoistConstantArrayArgumentsAnalyzer.TryGetCandidateInitializer(creation, out var initializer)).IsTrue();
        await Assert.That(initializer).IsSameReferenceAs(creation.Initializer);
    }
}
