// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1118TakeExtremeWithoutSortingAnalyzer,
    PerformanceSharp.Analyzers.Psh1118TakeExtremeWithoutSortingCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1118TakeExtremeWithoutSortingAnalyzer"/> (PSH1118 extreme element without sorting).</summary>
public class TakeExtremeWithoutSortingAnalyzerUnitTest
{
    /// <summary>Verifies an ascending sort taking the first value-typed element is flagged and rewritten to MinBy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderByFirstOnValueElementsIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public (int Key, int Value) M(List<(int Key, int Value)> items) => items.OrderBy(x => x.Key).{|PSH1118:First|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public (int Key, int Value) M(List<(int Key, int Value)> items) => items.MinBy(x => x.Key);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a descending sort taking the last element is flagged and rewritten to MinBy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderByDescendingLastIsFlaggedAndFixedToMinByAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(int[] values) => values.OrderByDescending(x => x % 10).{|PSH1118:Last|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(int[] values) => values.MinBy(x => x % 10);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an identity sort taking the first element is flagged and rewritten to Min.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityOrderByFirstIsFlaggedAndFixedToMinAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(int[] values) => values.OrderBy(x => x).{|PSH1118:First|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(int[] values) => values.Min();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies FirstOrDefault on reference-typed elements is flagged and rewritten to MinBy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceElementsWithFirstOrDefaultIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public string M(string[] values) => values.OrderBy(x => x.Length).{|PSH1118:FirstOrDefault|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public string M(string[] values) => values.MinBy(x => x.Length);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies First on reference-typed elements stays clean; MinBy would return null where First throws.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceElementsWithFirstIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Linq;

            public class C
            {
                public string M(string[] values) => values.OrderBy(x => x.Length).First();
            }
            """);

    /// <summary>Verifies FirstOrDefault on value-typed elements stays clean; MinBy would throw where FirstOrDefault returns default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueElementsWithFirstOrDefaultIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Linq;

            public class C
            {
                public int M(int[] values) => values.OrderBy(x => x % 10).FirstOrDefault();
            }
            """);

    /// <summary>Verifies a ThenBy between the sort and the terminal stays clean; the secondary sort still matters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThenByChainIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Linq;

            public class C
            {
                public int M(int[] values) => values.OrderBy(x => x % 10).ThenBy(x => x).First();
            }
            """);

    /// <summary>Verifies a terminal carrying a predicate stays clean; the predicate filters after the sort.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateTerminalIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Linq;

            public class C
            {
                public int M(int[] values) => values.OrderBy(x => x % 10).First(x => x > 1);
            }
            """);

    /// <summary>Verifies a sort carrying a comparer argument stays clean; MinBy(k) would drop the comparer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparerOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(int[] values) => values.OrderBy(x => x % 10, Comparer<int>.Default).First();
            }
            """);

    /// <summary>Verifies keyed suggestions stay silent on frameworks without Enumerable.MinBy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MinBySuggestionIsGatedOnApiExistingAsync()
        => await VerifyNet50Async(
            """
            using System.Linq;

            public class C
            {
                public int M(int[] values) => values.OrderBy(x => x % 10).First();
            }
            """);

    /// <summary>Verifies the identity rewrite to Min still fires on frameworks without Enumerable.MinBy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityRewriteIsNotGatedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(int[] values) => values.OrderBy(x => x).{|PSH1118:First|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(int[] values) => values.Min();
                                   }
                                   """;
        await VerifyNet50Async(Source, FixedSource);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifyNet90Async(string source, string? fixedSource = null)
        => VerifyAsync(ReferenceAssemblies.Net.Net90, source, fixedSource);

    /// <summary>Runs a verification against the .NET 5 reference assemblies, which lack Enumerable.MinBy.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifyNet50Async(string source, string? fixedSource = null)
        => VerifyAsync(ReferenceAssemblies.Net.Net50, source, fixedSource);

    /// <summary>Runs a verification against the supplied reference assemblies.</summary>
    /// <param name="referenceAssemblies">The reference assemblies to compile against.</param>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(ReferenceAssemblies referenceAssemblies, string source, string? fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = referenceAssemblies,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
