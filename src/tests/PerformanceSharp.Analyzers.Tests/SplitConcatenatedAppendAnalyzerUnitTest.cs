// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifySplitAppend = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1214SplitConcatenatedAppendAnalyzer,
    PerformanceSharp.Analyzers.Psh1214SplitConcatenatedAppendCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1214 (append the parts, not a concatenated whole) and its code fix.</summary>
public class SplitConcatenatedAppendAnalyzerUnitTest
{
    /// <summary>Verifies Append with a two-part concatenation is reported (PSH1214) and split into chained Appends.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendConcatenationSplitAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string name)
                                      => builder.Append({|PSH1214:name + ":"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string name)
                                           => builder.Append(name).Append(":");
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies AppendLine keeps the original method on the last part only.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendLineConcatenationSplitAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string a, string b)
                                      => builder.AppendLine({|PSH1214:a + b|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string a, string b)
                                           => builder.Append(a).AppendLine(b);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a three-operand chain is flattened into three appends in source order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeOperandChainSplitAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string a, string b, string c)
                                      => builder.Append({|PSH1214:a + b + c|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string a, string b, string c)
                                           => builder.Append(a).Append(b).Append(c);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a non-string operand still splits — the value lands on a typed Append overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendNonStringOperandSplitAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string name)
                                      => builder.Append({|PSH1214:name + 1|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string name)
                                           => builder.Append(name).Append(1);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an arithmetic prefix stays one operand — only the string-concatenation spine is unrolled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArithmeticPrefixKeptTogetherAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, int i, int j, string name)
                                      => builder.Append({|PSH1214:i + j + name|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, int i, int j, string name)
                                           => builder.Append(i + j).Append(name);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies chained appends are each reported and both split, including in one Fix All pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedCallsBothSplitAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string a, string b, string c, string d)
                                      => builder.Append({|PSH1214:a + b|}).AppendLine({|PSH1214:c + d|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string a, string b, string c, string d)
                                           => builder.Append(a).Append(b).Append(c).AppendLine(d);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an all-constant concatenation is not reported — the compiler folds it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantConcatenationIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append("a" + "b");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a plain single-value append is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainAppendIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string name)
                                      => builder.Append(name);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the multi-argument segment overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SegmentAppendOverloadIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string a, string b)
                                      => builder.Append(a + b, 0, 1);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a user-defined <c>+</c> operator returning string is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedOperatorIsCleanAsync()
    {
        const string Source = """
                              public class Wrapper
                              {
                                  public static string operator +(Wrapper left, string right) => right;
                              }

                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, Wrapper wrapper)
                                      => builder.Append(wrapper + "x");
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
        var test = new VerifySplitAppend.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
