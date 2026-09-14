// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyClear = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1127ClearOverFillDefaultAnalyzer,
    PerformanceSharp.Analyzers.Psh1127ClearOverFillDefaultCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1127 (clear an array instead of filling it with its default) and its code fix.</summary>
public class ClearOverFillDefaultAnalyzerUnitTest
{
    /// <summary>Verifies the current syntax gate ignores explicit type arguments on an unqualified Fill call.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnqualifiedGenericFillIsCurrentlyCleanAsync() =>
        new VerifyClear.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                using static System.Array;
                class C
                {
                    void M(int[] values)
                    {
                        Fill<int>(values, 0);
                        System.Array.Resize(ref values, 0);
                        Resize(ref values, 0);
                    }
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a null array argument does not supply an element type for a literal default.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UntypedNullArrayIsCleanAsync() =>
        new VerifyClear.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = "class C { void M() { System.Array.Fill<int>(null, 0); } }" }.RunAsync(CancellationToken.None);

    /// <summary>Verifies framework versions without Fill reject lookalike calls before binding.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FrameworkWithoutFillIsCleanAsync() =>
        new VerifyClear.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = """
                class Array { public static void Fill<T>(T[] array, T value) { } }
                class C { void M(int[] values) { Array.Fill(values, 0); } }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies fallback repetition is restricted to simple name chains.</summary>
    /// <param name="expression">The expression being considered for repeated evaluation.</param>
    /// <param name="expected">Whether the expression can be repeated.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("values", true)]
    [Arguments("this.values", true)]
    [Arguments("this", true)]
    [Arguments("int.MaxValue", true)]
    [Arguments("values[0]", false)]
    [Arguments("GetValues()", false)]
    [Arguments("(values)", false)]
    public async Task RepeatableSyntaxIsRestrictedAsync(string expression, bool expected)
    {
        var syntax = SyntaxFactory.ParseExpression(expression);
        await Assert.That(Psh1127ClearOverFillDefaultAnalyzer.IsRepeatableExpression(syntax)).IsEqualTo(expected);
    }

    /// <summary>Verifies qualified and using-static calls and typed defaults are recognized.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DefaultFillShapesAreRecognizedAsync() =>
        new VerifyClear.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                using System;
                using static System.Array;
                class C
                {
                    void M(int[] values, int?[] nullable, object[] boxed)
                    {
                        {|PSH1127:System.Array.Fill(values, default(int))|};
                        {|PSH1127:Fill(values, 0)|};
                        {|PSH1127:Array.Fill(nullable, null)|};
                        Array.Fill(nullable, 0);
                        Array.Fill(boxed, false);
                        Array.Fill(values, 1 + 0);
                        Array.Fill(values, default(int) + 1);
                    }
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies older array APIs require a repeatable whole-array expression but allow ranged calls.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RangedClearFallbackRequiresRepeatableArrayAsync() =>
        VerifyClear.VerifyAnalyzerAsync("""
            namespace System
            {
                public class Array
                {
                    public static void Fill<T>(T[] array, T value) { }
                    public static void Fill<T>(T[] array, T value, int start, int count) { }
                    public static void Clear<T>(T[] array, int start, int count) { }
                }
            }
            class C
            {
                int[] values;
                int[] GetValues() => values;
                void M()
                {
                    {|PSH1127:System.Array.Fill(values, 0)|};
                    {|PSH1127:System.Array.Fill(this.values, 0)|};
                    System.Array.Fill(GetValues(), 0);
                    System.Array.Fill((values), 0);
                    {|PSH1127:System.Array.Fill(GetValues(), 0, 0, 1)|};
                }
            }
            """);

    /// <summary>Verifies Fill support alone cannot justify suggesting an absent Clear API.</summary>
    /// <param name="clearMember">A member that does not supply a static Clear method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public static int Clear;")]
    [Arguments("public void Clear() { }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MissingClearApiIsCleanAsync(string clearMember) =>
        VerifyClear.VerifyAnalyzerAsync($$"""
            namespace System
            {
                public class Array
                {
                    public static void Fill<T>(T[] array, T value) { }
                    {{clearMember}}
                }
            }
            class C { void M(int[] values) { System.Array.Fill(values, 0); } }
            """);

    /// <summary>Verifies instance lookalikes, unrelated receivers, and nondefault literals are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedFillCallsAreCleanAsync() =>
        new VerifyClear.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                class Helper { public void Fill(int[] values, int value) { } }
                class C
                {
                    void M(Helper Array, int[] values)
                    {
                        Array.Fill(values, 0);
                        new Helper().Fill(values, 0);
                        System.Array.Fill(values, 42);
                        System.Array.Fill(new[] { true }, true);
                        System.Array.Fill(new[] { "text" }, "");
                    }
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies filling an int array with 0 is reported and rewritten to Array.Clear.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FillWithZeroReplacedWithClearAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int[] buffer) => {|PSH1127:Array.Fill(buffer, 0)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int[] buffer) => Array.Clear(buffer);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies filling with the default literal is reported and rewritten to Array.Clear.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FillWithDefaultReplacedWithClearAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(double[] buffer) => {|PSH1127:Array.Fill(buffer, default)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(double[] buffer) => Array.Clear(buffer);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies filling a reference array with null is reported and rewritten to Array.Clear.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FillWithNullReplacedWithClearAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string[] names) => {|PSH1127:Array.Fill(names, null)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string[] names) => Array.Clear(names);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the ranged Fill overload is rewritten to the ranged Clear overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RangedFillReplacedWithRangedClearAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int[] buffer, int start, int count) => {|PSH1127:Array.Fill(buffer, 0, start, count)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int[] buffer, int start, int count) => Array.Clear(buffer, start, count);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies filling a bool array with false is reported and rewritten to Array.Clear.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FillWithFalseReplacedWithClearAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(bool[] flags) => {|PSH1127:Array.Fill(flags, false)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(bool[] flags) => Array.Clear(flags);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies filling an object array with 0 is never reported: a boxed zero is not null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FillObjectArrayWithZeroIsNotReportedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(object[] values) => Array.Fill(values, 0);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies filling with a non-default value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FillWithNonDefaultValueIsNotReportedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int[] buffer) => Array.Fill(buffer, 5);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyClear.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }
}
