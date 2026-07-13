// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyModifier = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1491RedundantModifierAnalyzer,
    StyleSharp.Analyzers.Sst1491RedundantModifierCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1491 (modifiers should not restate the default) and its fix.</summary>
public class Sst1491RedundantModifierAnalyzerUnitTest
{
    /// <summary>Verifies public on an interface member is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicOnInterfaceMemberIsRemovedAsync()
    {
        const string Source = """
                              public interface IService
                              {
                                  {|SST1491:public|} int Value { get; }

                                  {|SST1491:public|} void Run();
                              }
                              """;
        const string FixedSource = """
                                   public interface IService
                                   {
                                       int Value { get; }

                                       void Run();
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies abstract on a bodiless interface member, and virtual on one with a body, are removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractAndVirtualOnInterfaceMembersAreRemovedAsync()
    {
        const string Source = """
                              public interface IService
                              {
                                  {|SST1491:abstract|} void Run();

                                  {|SST1491:virtual|} int Fallback() => 0;
                              }
                              """;
        const string FixedSource = """
                                   public interface IService
                                   {
                                       void Run();

                                       int Fallback() => 0;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies two redundant modifiers on one member are both reported and both removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoRedundantModifiersOnOneMemberAreBothRemovedAsync()
    {
        const string Source = """
                              public interface IService
                              {
                                  {|SST1491:public|} {|SST1491:abstract|} void Run();
                              }
                              """;
        const string FixedSource = """
                                   public interface IService
                                   {
                                       void Run();
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an indexer and an event on an interface are measured like any other member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryInterfaceMemberKindIsMeasuredAsync()
        => await RunAsync(
            """
            using System;

            public interface IService
            {
                {|SST1491:public|} int this[int index] { get; }

                {|SST1491:public|} event EventHandler Changed;
            }
            """);

    /// <summary>Verifies a private interface member keeps its modifier, which is not the default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Interface members are public, so dropping private would widen the member instead of tidying it.</remarks>
    [Test]
    public async Task PrivateOnInterfaceMemberIsCleanAsync()
        => await RunAsync(
            """
            public interface IService
            {
                int Value => Compute();

                private int Compute() => 1;
            }
            """);

    /// <summary>Verifies a static interface member keeps abstract and virtual, which mean something there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A static member with a body is not virtual unless it says so, and one without a body must say abstract.</remarks>
    [Test]
    public async Task StaticInterfaceMemberKeepsAbstractAndVirtualAsync()
        => await RunAsync(
            """
            public interface ICounter<TSelf>
                where TSelf : ICounter<TSelf>
            {
                static abstract TSelf Zero { get; }

                static virtual int Step() => 1;
            }
            """);

    /// <summary>Verifies public on a static interface member is still reported; accessibility does not change.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicOnStaticInterfaceMemberIsReportedAsync()
        => await RunAsync(
            """
            public interface ICounter
            {
                {|SST1491:public|} static int Step() => 1;
            }
            """);

    /// <summary>Verifies a sealed interface member is left alone; sealed makes a member non-virtual there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SealedInterfaceMemberIsCleanAsync()
        => await RunAsync(
            """
            public interface IService
            {
                sealed int Fixed() => 1;
            }
            """);

    /// <summary>Verifies readonly on a member of a readonly struct is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyOnMemberOfReadOnlyStructIsRemovedAsync()
    {
        const string Source = """
                              public readonly struct Point
                              {
                                  public {|SST1491:readonly|} int X => 0;

                                  public {|SST1491:readonly|} int Sum() => X;
                              }
                              """;
        const string FixedSource = """
                                   public readonly struct Point
                                   {
                                       public int X => 0;

                                       public int Sum() => X;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies readonly on a member of a mutable struct is kept; there it does something.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyOnMemberOfMutableStructIsCleanAsync()
        => await RunAsync(
            """
            public struct Point
            {
                private int _x;

                public readonly int X => _x;

                public void Move(int x) => _x = x;
            }
            """);

    /// <summary>Verifies a static class's members keep static, which they cannot compile without.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>An instance member of a static class does not compile, so the modifier is required, not redundant.</remarks>
    [Test]
    public async Task StaticOnMemberOfStaticClassIsCleanAsync()
        => await RunAsync(
            """
            public static class Helpers
            {
                public static int Value => 0;

                public static int Twice(int value) => value * 2;
            }
            """);

    /// <summary>Verifies a sealed member is left to the rule that already reports it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SealedMemberIsNotThisRulesJobAsync()
        => await RunAsync(
            """
            public class Base
            {
                public virtual void Run()
                {
                }
            }

            public sealed class Derived : Base
            {
                public sealed override void Run()
                {
                }
            }
            """);

    /// <summary>Verifies an ordinary class member's modifiers are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinaryClassMemberIsCleanAsync()
        => await RunAsync(
            """
            public abstract class Service
            {
                public virtual int Value => 0;

                public abstract void Run();

                private int Compute() => 1;
            }
            """);

    /// <summary>Verifies an unsafe member inside an unsafe type is reported and its modifier removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedUnsafeModifierIsRemovedAsync()
    {
        const string Source = """
                              public unsafe class Native
                              {
                                  public {|SST1491:unsafe|} int First(int* values) => *values;
                              }
                              """;
        const string FixedSource = """
                                   public unsafe class Native
                                   {
                                       public int First(int* values) => *values;
                                   }
                                   """;
        await RunUnsafeAsync(Source, FixedSource);
    }

    /// <summary>Verifies an unsafe member outside any unsafe context is left to the rule that owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Nothing encloses this member's unsafe modifier, so the modifier is doing the work.</remarks>
    [Test]
    public async Task UnsafeModifierWithNoEnclosingUnsafeIsCleanAsync()
        => await RunUnsafeAsync(
            """
            public class Native
            {
                public unsafe int First(int* values) => *values;
            }
            """);

    /// <summary>Verifies a nested unsafe member that contains no unsafe syntax is left to the rule that owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>That modifier is unnecessary for a different reason, and reporting it under two ids helps nobody.</remarks>
    [Test]
    public async Task NestedUnsafeWithoutUnsafeSyntaxIsCleanAsync()
        => await RunUnsafeAsync(
            """
            public unsafe class Native
            {
                public unsafe int Nothing() => 0;
            }
            """);

    /// <summary>Runs one analyzer or code-fix case against the modern reference assemblies.</summary>
    /// <summary>Verifies the abstract on a re-abstracted explicit interface member is not reported.</summary>
    /// <remarks>
    /// Nothing about that declaration is implied. Re-abstracting an inherited member requires the
    /// <c>abstract</c>, and taking it away is CS0501 — the declaration then has to carry a body.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractOnReAbstractedExplicitInterfaceMemberIsNotReportedAsync()
        => await RunAsync(
            """
            public interface IFoo
            {
                int Bar();
            }

            public interface IBar : IFoo
            {
                abstract int IFoo.Bar();
            }
            """);

    /// <summary>Runs the analyzer, and its fix when one is expected.</summary>
    /// <param name="source">The test source, with its expected diagnostics marked up.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix is expected.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Default interface members and static abstract members need a runtime that supports them.</remarks>
    private static async Task RunAsync(string source, string? fixedSource = null)
    {
        var test = new VerifyModifier.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs one case in a project that allows the pointer syntax the unsafe tests declare.</summary>
    /// <param name="source">The test source, with its expected diagnostics marked up.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix is expected.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunUnsafeAsync(string source, string? fixedSource = null)
    {
        var test = new VerifyModifier.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var options = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
