// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifySpecifyStringComparison = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1207SpecifyStringComparisonAnalyzer,
    PerformanceSharp.Analyzers.Psh1207SpecifyStringComparisonCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1207 (specify StringComparison for culture-sensitive string operations) and its code fix.</summary>
public class SpecifyStringComparisonAnalyzerUnitTest
{
    /// <summary>Verifies a bare <c>StartsWith(string)</c> is reported and gains an ordinal comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartsWithGainsOrdinalAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.{|PSH1207:StartsWith|}(right);
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => left.StartsWith(right, System.StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a bare <c>EndsWith(string)</c> is reported and gains an ordinal comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EndsWithGainsOrdinalAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.{|PSH1207:EndsWith|}(right);
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => left.EndsWith(right, System.StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a bare <c>IndexOf(string)</c> is reported and gains an ordinal comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexOfGainsOrdinalAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string left, string right)
                                      => left.{|PSH1207:IndexOf|}(right);
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string left, string right)
                                           => left.IndexOf(right, System.StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a bare <c>LastIndexOf(string)</c> is reported and gains an ordinal comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LastIndexOfGainsOrdinalAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string left, string right)
                                      => left.{|PSH1207:LastIndexOf|}(right);
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string left, string right)
                                           => left.LastIndexOf(right, System.StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies static <c>string.Compare(string, string)</c> is reported and gains an ordinal comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticCompareGainsOrdinalAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string left, string right)
                                      => string.{|PSH1207:Compare|}(left, right);
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string left, string right)
                                           => string.Compare(left, right, System.StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a call that already specifies a StringComparison is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitComparisonIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.StartsWith(right, System.StringComparison.Ordinal);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies <c>Contains(string)</c>, which is ordinal by default, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.Contains(right);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the char overload of a search method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CharOverloadIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string left)
                                      => left.IndexOf('x');
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a non-string receiver with its own <c>StartsWith(string)</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringReceiverIsCleanAsync()
    {
        const string Source = """
                              public class Wrapper
                              {
                                  public bool StartsWith(string value) => true;
                              }

                              public class C
                              {
                                  public bool M(Wrapper left, string right)
                                      => left.StartsWith(right);
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
        var test = new VerifySpecifyStringComparison.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
