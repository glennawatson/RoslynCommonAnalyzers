// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1014ReadonlyStructAnalyzer,
    PerformanceSharp.Analyzers.Psh1014ReadonlyStructCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1014ReadonlyStructAnalyzer"/> (PSH1014 readonly structs).</summary>
public class ReadonlyStructAnalyzerUnitTest
{
    /// <summary>Verifies event, buffer, nested-type, and this-expression shapes preserve their mutability contract.</summary>
    /// <param name="members">The members of the struct to inspect.</param>
    /// <param name="expected">Whether the instance state is immutable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public event System.Action Changed;", false)]
    [Arguments("public static event System.Action Changed;", true)]
    [Arguments("public class Nested { public int Value; }", true)]
    [Arguments("public enum Nested { First }", true)]
    [Arguments("public fixed int Values[4];", false)]
    [Arguments("public int Value { get => 0; set { this = default; } }", false)]
    [Arguments("public int Value { get => 0; set => this = default; }", false)]
    [Arguments("public int Value { get => 0; set { } }", true)]
    [Arguments("public int Value { get; init; }", true)]
    [Arguments("public static int Value { get; set; }", true)]
    [Arguments("public void M() { Consume(ref this); }", false)]
    [Arguments("public void M() { Consume(out this); }", false)]
    [Arguments("public void M() { Consume(this); }", true)]
    [Arguments("public void M() { var copy = this; }", true)]
    [Arguments("public void M() { S copy; copy = this; }", true)]
    [Arguments("public int this[int index] => index;", true)]
    public async Task MemberShapesDetermineImmutabilityAsync(string members, bool expected)
    {
        var declaration = (Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($$"""struct S { {{members}} }""")!;
        await Assert.That(Psh1014ReadonlyStructAnalyzer.HasImmutableInstanceState(declaration)).IsEqualTo(expected);
    }

    /// <summary>Verifies the modifier is not suggested before its language version or on excluded declarations.</summary>
    /// <param name="source">The declaration to analyze.</param>
    /// <param name="version">The language version available to the declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public struct S { }", LanguageVersion.CSharp7_1)]
    [Arguments("public readonly struct S { }", LanguageVersion.CSharp7_2)]
    [Arguments("public partial struct S { }", LanguageVersion.CSharp7_2)]
    public async Task ExcludedDeclarationsAreCleanAsync(string source, LanguageVersion version)
    {
        var test = new Verify.Test { TestCode = source };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectParseOptions(projectId, new CSharpParseOptions(version)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a struct with only readonly fields is flagged and gains the modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableStructIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public struct {|PSH1014:Point|}
                              {
                                  private readonly int _x;
                                  private readonly int _y;

                                  public Point(int x, int y)
                                  {
                                      _x = x;
                                      _y = y;
                                  }

                                  public int X => _x;

                                  public int Y => _y;
                              }
                              """;
        const string FixedSource = """
                                   public readonly struct Point
                                   {
                                       private readonly int _x;
                                       private readonly int _y;

                                       public Point(int x, int y)
                                       {
                                           _x = x;
                                           _y = y;
                                       }

                                       public int X => _x;

                                       public int Y => _y;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies get-only auto-properties count as immutable state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetOnlyAutoPropertyStructIsFlaggedAsync()
    {
        const string Source = """
                              public struct {|PSH1014:Size|}
                              {
                                  public Size(int width) => Width = width;

                                  public int Width { get; }
                              }
                              """;
        const string FixedSource = """
                                   public readonly struct Size
                                   {
                                       public Size(int width) => Width = width;

                                       public int Width { get; }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mutable field keeps the struct clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MutableFieldIsCleanAsync() =>
        VerifyAsync(
            """
            public struct Accumulator
            {
                private int _total;

                public void Add(int value) => _total += value;

                public int Total => _total;
            }
            """);

    /// <summary>Verifies a settable auto-property keeps the struct clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SettableAutoPropertyIsCleanAsync() =>
        VerifyAsync(
            """
            public struct Box
            {
                public int Value { get; set; }
            }
            """);

    /// <summary>Verifies a method reassigning <c>this</c> keeps the struct clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThisReassignmentIsCleanAsync() =>
        VerifyAsync(
            """
            public struct Resettable
            {
                private readonly int _value;

                public Resettable(int value) => _value = value;

                public int Value => _value;

                public void Reset() => this = default;
            }
            """);

    /// <summary>Verifies a record struct with a primary constructor stays clean; its properties are settable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalRecordStructIsCleanAsync() =>
        VerifyAsync(
            """
            public record struct Pair(int First, int Second);
            """);

    /// <summary>Verifies the readonly struct modifier is offered on C# 7.2, the version that introduced it (regression against an over-strict 7.3 gate).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableStructIsFlaggedOnCSharp72Async()
    {
        const string Source = """
                              public struct {|PSH1014:Point|}
                              {
                                  private readonly int _x;

                                  public Point(int x) => _x = x;

                                  public int X => _x;
                              }
                              """;
        const string FixedSource = """
                                   public readonly struct Point
                                   {
                                       private readonly int _x;

                                       public Point(int x) => _x = x;

                                       public int X => _x;
                                   }
                                   """;

        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = FixedSource };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7_2));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
