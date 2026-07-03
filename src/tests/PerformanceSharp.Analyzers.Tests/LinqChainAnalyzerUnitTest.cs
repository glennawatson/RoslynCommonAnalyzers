// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyLinqChain = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.LinqChainAnalyzer,
    PerformanceSharp.Analyzers.LinqChainCodeFixProvider>;
using VerifyLinqChainAnalyzer = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.LinqChainAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for the LINQ chain rules (PSH1107, PSH1108, PSH1109) and their fixes.</summary>
public class LinqChainAnalyzerUnitTest
{
    /// <summary>Verifies a Where after a single-key OrderBy is reported (PSH1107) and swapped before the sort.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereAfterOrderByIsSwappedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.OrderBy(value => value).{|PSH1107:Where|}(value => value > 0);
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(int[] values) => values.Where(value => value > 0).OrderBy(value => value);
                                   }
                                   """;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a Where after a multi-key ThenBy chain is reported (PSH1107) without an automated fix.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereAfterThenByIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.OrderBy(value => value % 2).ThenBy(value => value).{|PSH1107:Where|}(value => value > 0);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a repeated OrderBy is reported (PSH1108) and renamed to ThenBy.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedOrderByUsesThenByAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.OrderBy(value => value % 2).{|PSH1108:OrderBy|}(value => value);
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(int[] values) => values.OrderBy(value => value % 2).ThenBy(value => value);
                                   }
                                   """;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a repeated OrderByDescending is reported (PSH1108) and renamed to ThenByDescending.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RepeatedOrderByDescendingUsesThenByDescendingAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.OrderBy(value => value % 2).{|PSH1108:OrderByDescending|}(value => value);
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(int[] values) => values.OrderBy(value => value % 2).ThenByDescending(value => value);
                                   }
                                   """;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an OrderBy followed by ThenBy is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OrderByThenByIsCleanAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.OrderBy(value => value % 2).ThenBy(value => value);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies consecutive Where calls with the same parameter name are reported (PSH1109) and merged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConsecutiveWhereWithSameParameterIsMergedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.Where(value => value > 0).{|PSH1109:Where|}(value => value < 10);
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(int[] values) => values.Where(value => (value > 0) && (value < 10));
                                   }
                                   """;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies consecutive Where calls with different parameter names are merged with a rename.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConsecutiveWhereWithDifferentParameterIsMergedWithRenameAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.Where(left => left > 0).{|PSH1109:Where|}(right => right < 10);
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(int[] values) => values.Where(left => (left > 0) && (left < 10));
                                   }
                                   """;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies statement-bodied Where predicates are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StatementBodiedWhereIsCleanAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M1(int[] values) => values.Where(value => { return value > 0; }).Where(value => value < 10);

                                  public object M2(int[] values) => values.Where(value => value > 0).Where(value => { return value < 10; });
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies the index-taking Where overload is not reported by any of the three rules.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IndexOverloadWhereIsCleanAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M1(int[] values) => values.OrderBy(value => value).Where((value, index) => value > index);

                                  public object M2(int[] values) => values.Where((value, index) => value > index).Where(value => value < 10);

                                  public object M3(int[] values) => values.Where(value => value > 0).Where((value, index) => value > index);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies IQueryable chains stay clean for all three rules.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QueryableChainsAreCleanAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M1(IQueryable<int> values) => values.OrderBy(value => value).Where(value => value > 0);

                                  public object M2(IQueryable<int> values) => values.OrderBy(value => value % 2).OrderBy(value => value);

                                  public object M3(IQueryable<int> values) => values.Where(value => value > 0).Where(value => value < 10);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a sort-filter-filter chain reports PSH1107 on the first Where and PSH1109 on the second.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CombinedChainReportsFilterAndMergeAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.OrderBy(value => value).{|PSH1107:Where|}(value => value > 0).{|PSH1109:Where|}(value => value < 10);
                              }
                              """;
        await VerifyAnalyzerAsync(Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        var test = new VerifyLinqChain.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new VerifyLinqChainAnalyzer.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
