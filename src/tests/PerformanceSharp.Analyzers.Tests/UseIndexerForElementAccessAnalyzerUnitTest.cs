// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyIndexer = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1106UseIndexerForElementAccessAnalyzer,
    PerformanceSharp.Analyzers.Psh1106UseIndexerForElementAccessCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1106 (index collections directly instead of using LINQ element access) and its code fix.</summary>
public class UseIndexerForElementAccessAnalyzerUnitTest
{
    /// <summary>Verifies First() on a list is reported (PSH1106) and fixed to the zero indexer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListFirstReplacedWithIndexerAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(List<int> list) => {|PSH1106:list.First()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(List<int> list) => list[0];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies First() on an array is reported and fixed to the zero indexer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayFirstReplacedWithIndexerAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(int[] values) => {|PSH1106:values.First()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(int[] values) => values[0];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies ElementAt(index) on a list is reported and fixed to the indexer with the same argument.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListElementAtReplacedWithIndexerAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(List<int> list, int index) => {|PSH1106:list.ElementAt(index)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(List<int> list, int index) => list[index];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Last() on a list is reported and fixed to a Count-based indexer read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListLastReplacedWithCountIndexAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(List<int> list) => {|PSH1106:list.Last()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(List<int> list) => list[list.Count - 1];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Last() on an array is reported and fixed to a Length-based indexer read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayLastReplacedWithLengthIndexAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(int[] values) => {|PSH1106:values.Last()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(int[] values) => values[values.Length - 1];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an IReadOnlyList parameter receiver is reported and fixed to the indexer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyListInterfaceReceiverReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IReadOnlyList<int> items) => {|PSH1106:items.First()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(IReadOnlyList<int> items) => items[0];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every reported element access in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class A
                              {
                                  public int M(List<int> list) => {|PSH1106:list.First()|};
                              }

                              public class B
                              {
                                  public int M(List<int> list) => {|PSH1106:list.Last()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class A
                                   {
                                       public int M(List<int> list) => list[0];
                                   }

                                   public class B
                                   {
                                       public int M(List<int> list) => list[list.Count - 1];
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Last() on a method-call receiver is still reported but produces no code-fix change.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComplexReceiverLastReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M() => {|PSH1106:Create().Last()|};

                                  public List<int> Create() => new List<int>();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a plain IEnumerable receiver is not reported because no indexer exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumerableReceiverIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IEnumerable<int> source) => source.First();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the predicate First overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateFirstIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(List<int> list) => list.First(x => x > 1);
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
        var test = new VerifyIndexer.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
