// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyReadonlyStructMember = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1460ReadonlyStructMemberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1460ReadonlyStructMemberAnalyzer"/>.</summary>
public class ReadonlyStructMemberAnalyzerUnitTest
{
    /// <summary>Checks constructor arguments passed by value do not imply receiver mutation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConstructorValueArgumentIsReportedAsync() =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync("struct C { public object {|SST1460:M|}() => new Box(1); } class Box { public Box(int value) {} }");

    /// <summary>Checks block bodies and get-only properties are eligible.</summary>
    /// <param name="member">The member with expected diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public int {|SST1460:Value|}() { return 1; }")]
    [Arguments("public int {|SST1460:Value|} => 1;")]
    [Arguments("public int {|SST1460:Value|} { get { return 1; } }")]
    [Arguments("public int {|SST1460:Value|} { get; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReadOnlyBodiesAreReportedAsync(string member) =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync($$"""public struct Counter { {{member}} }""");

    /// <summary>Checks operations that may mutate the receiver keep methods and getters unreported.</summary>
    /// <param name="operation">The potentially mutating statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_value = 1;")]
    [Arguments("_value += 1;")]
    [Arguments("_value -= 1;")]
    [Arguments("_value *= 2;")]
    [Arguments("_value /= 2;")]
    [Arguments("_value %= 2;")]
    [Arguments("_value &= 1;")]
    [Arguments("_value ^= 1;")]
    [Arguments("_value |= 1;")]
    [Arguments("_value <<= 1;")]
    [Arguments("_value >>= 1;")]
    [Arguments("++_value;")]
    [Arguments("--_value;")]
    [Arguments("_value++;")]
    [Arguments("_value--;")]
    [Arguments("_text ??= string.Empty;")]
    [Arguments("var box = new Box(ref _value);")]
    [Arguments("var box = new OutBox(out _value);")]
    [Arguments("var box = new InBox(in _value);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PotentialMutationIsCleanAsync(string operation) =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync($$"""
            public struct Counter
            {
                private int _value;
                private string _text;
                public int Value() { {{operation}} return _value; }
                public int Property { get { {{operation}} return _value; } }
            }
            public class Box { public Box(ref int value) {} }
            public class OutBox { public OutBox(out int value) { value = 0; } }
            public class InBox { public InBox(in int value) {} }
            """);

    /// <summary>Checks excluded containers, modifiers, setters, and incomplete declarations.</summary>
    /// <param name="source">The source that cannot receive a readonly suggestion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int M() => 1; int P => 1; }")]
    [Arguments("readonly struct C { int M() => 1; int P => 1; }")]
    [Arguments("struct C { readonly int M() => 1; readonly int P => 1; }")]
    [Arguments("struct C { static int M() => 1; static int P => 1; }")]
    [Arguments("struct C { abstract int M(); abstract int P { get; } }")]
    [Arguments("struct C { extern int M(); extern int P { get; } }")]
    [Arguments("partial struct C { partial int M(); partial int P { get; } }")]
    [Arguments("struct C { int M(); int P }")]
    [Arguments("struct C { int P { get; set; } int Q { get; init; } }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task IneligibleMembersAreCleanAsync(string source) =>
        new VerifyReadonlyStructMember.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a non-mutating struct method is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonMutatingMethodIsReportedAsync() =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                public int {|SST1460:Value|}() => _value;
            }
            """);

    /// <summary>Verifies a method with a call is skipped because mutation cannot be cheaply proven away.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodWithCallIsCleanAsync() =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                public int Value() => GetValue();
                private readonly int GetValue() => 1;
            }
            """);

    /// <summary>Verifies a property returning a writable reference is skipped, since readonly would not compile.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RefReturningPropertyIsCleanAsync() =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                [System.Diagnostics.CodeAnalysis.UnscopedRef]
                public ref int Value => ref _value;
            }
            """);

    /// <summary>Verifies a method returning a writable reference is skipped, since readonly would not compile.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RefReturningMethodIsCleanAsync() =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                [System.Diagnostics.CodeAnalysis.UnscopedRef]
                public ref int Value() => ref _value;
            }
            """);

    /// <summary>Verifies a property returning a readonly reference is still reported, which stays compilable.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RefReadonlyReturningPropertyIsReportedAsync() =>
        VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                [System.Diagnostics.CodeAnalysis.UnscopedRef]
                public ref readonly int {|SST1460:Value|} => ref _value;
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 8, where readonly instance members do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp8Async()
    {
        const string Source = """
                              public struct Counter
                              {
                                  private int _value;

                                  public int Value() => _value;
                                  public int Property => _value;
                              }
                              """;
        var test = new VerifyReadonlyStructMember.Test { TestCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7_3));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
