// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCount = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1103UseCountPropertyAnalyzer,
    PerformanceSharp.Analyzers.Psh1103UseCountPropertyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1103 (prefer the collection's own count over enumerating) and its code fix.</summary>
public class UseCountPropertyAnalyzerUnitTest
{
    /// <summary>Verifies a parameterless Count() call on a list is reported (PSH1103) and fixed to the Count property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListCountReplacedWithCountPropertyAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(List<int> list) => {|PSH1103:list.Count()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(List<int> list) => list.Count;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a parameterless Count() call on an array is reported and fixed to the Length property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayCountReplacedWithLengthPropertyAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(int[] values) => {|PSH1103:values.Count()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(int[] values) => values.Length;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a parameterless Count() call on a string is reported and fixed to the Length property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringCountReplacedWithLengthPropertyAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(string text) => {|PSH1103:text.Count()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(string text) => text.Length;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a parameterless Any() call is reported and fixed to a Count comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListAnyReplacedWithCountComparisonAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => {|PSH1103:list.Any()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list) => list.Count > 0;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a negated Any() call rewrites the whole logical-not to a Count equality.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedAnyReplacedWithCountEqualityAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list)
                                  {
                                      if (!{|PSH1103:list.Any()|})
                                      {
                                          return true;
                                      }

                                      return false;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list)
                                       {
                                           if (list.Count == 0)
                                           {
                                               return true;
                                           }

                                           return false;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the Any() comparison is parenthesized when the call sits inside a larger expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnyInsideLargerExpressionParenthesizedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list, bool flag) => {|PSH1103:list.Any()|} && flag;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list, bool flag) => (list.Count > 0) && flag;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a custom enumerable with a public int Count property is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomCountedEnumerableReportedAsync()
    {
        const string Source = """
                              using System.Collections;
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class Bag : IEnumerable<int>
                              {
                                  public int Count => 0;

                                  public IEnumerator<int> GetEnumerator() => throw new System.NotSupportedException();

                                  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                              }

                              public class C
                              {
                                  public int M(Bag bag) => {|PSH1103:bag.Count()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections;
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public sealed class Bag : IEnumerable<int>
                                   {
                                       public int Count => 0;

                                       public IEnumerator<int> GetEnumerator() => throw new System.NotSupportedException();

                                       IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                                   }

                                   public class C
                                   {
                                       public int M(Bag bag) => bag.Count;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an IReadOnlyCollection receiver is reported because Count exists via the interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyCollectionInterfaceReceiverReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IReadOnlyCollection<int> collection) => {|PSH1103:collection.Count()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(IReadOnlyCollection<int> collection) => collection.Count;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every reported Enumerable call in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class A
                              {
                                  public int M(List<int> list) => {|PSH1103:list.Count()|};
                              }

                              public class B
                              {
                                  public bool M(List<int> list) => {|PSH1103:list.Any()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class A
                                   {
                                       public int M(List<int> list) => list.Count;
                                   }

                                   public class B
                                   {
                                       public bool M(List<int> list) => list.Count > 0;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a plain IEnumerable receiver is not reported because no constant-time count exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumerableReceiverIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IEnumerable<int> source) => source.Count();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the predicate Count overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateCountIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(List<int> list) => list.Count(x => x > 1);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the predicate Any overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateAnyIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => list.Any(x => x > 1);
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
        var test = new VerifyCount.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
