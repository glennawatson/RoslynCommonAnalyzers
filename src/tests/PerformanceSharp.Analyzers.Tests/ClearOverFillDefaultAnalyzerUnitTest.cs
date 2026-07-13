// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyClear = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1127ClearOverFillDefaultAnalyzer,
    PerformanceSharp.Analyzers.Psh1127ClearOverFillDefaultCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1127 (clear an array instead of filling it with its default) and its code fix.</summary>
public class ClearOverFillDefaultAnalyzerUnitTest
{
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
        var test = new VerifyClear.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
