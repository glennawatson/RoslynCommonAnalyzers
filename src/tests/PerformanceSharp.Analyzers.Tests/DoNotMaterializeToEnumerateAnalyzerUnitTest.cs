// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1120DoNotMaterializeToEnumerateAnalyzer,
    PerformanceSharp.Analyzers.Psh1120DoNotMaterializeToEnumerateCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1120DoNotMaterializeToEnumerateAnalyzer"/> (PSH1120 materialize-to-enumerate).</summary>
public class DoNotMaterializeToEnumerateAnalyzerUnitTest
{
    /// <summary>Verifies a ToList call at the end of a LINQ chain is flagged and the fix drops it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToListOnWhereChainIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IEnumerable<int> items)
                                  {
                                      var total = 0;
                                      foreach (var x in items.Where(i => i > 0).{|PSH1120:ToList|}())
                                      {
                                          total += x;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(IEnumerable<int> items)
                                       {
                                           var total = 0;
                                           foreach (var x in items.Where(i => i > 0))
                                           {
                                               total += x;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a ToArray call directly on the source is flagged and the fix drops it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArrayOnSourceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(IEnumerable<int> items)
                                  {
                                      var total = 0;
                                      foreach (var x in items.{|PSH1120:ToArray|}())
                                      {
                                          total += x;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(IEnumerable<int> items)
                                       {
                                           var total = 0;
                                           foreach (var x in items)
                                           {
                                               total += x;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a loop that mutates the materialized source stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutatedSourceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(List<int> items)
                {
                    foreach (var x in items.ToList())
                    {
                        if (x > 0)
                        {
                            items.Remove(x);
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies any mention of the source identifier in the loop body suppresses the report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodyMentionOfSourceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(List<int> items)
                {
                    var total = 0;
                    foreach (var x in items.ToList())
                    {
                        total += x + items.Count;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies the guard follows the root identifier through a LINQ chain to the source.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RootIdentifierGuardsThroughChainAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(List<int> items)
                {
                    foreach (var x in items.Where(i => i > 0).ToList())
                    {
                        items.Add(x);
                    }
                }
            }
            """);

    /// <summary>Verifies enumerating a variable that holds the materialized copy stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaterializedLocalIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> items)
                {
                    var total = 0;
                    var list = items.ToList();
                    foreach (var x in list)
                    {
                        total += x;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an await foreach stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitForeachIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class AsyncSequence : IAsyncEnumerable<int>
            {
                public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                    => throw new NotSupportedException();
            }

            public static class AsyncSequenceExtensions
            {
                public static AsyncSequence ToList(this AsyncSequence source) => source;
            }

            public class C
            {
                public async Task M(AsyncSequence items)
                {
                    await foreach (var x in items.ToList())
                    {
                        _ = x;
                    }
                }
            }
            """);

    /// <summary>Verifies a custom ToList extension that takes an argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToListWithArgumentIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public static class SequenceExtensions
            {
                public static List<int> ToList(this IEnumerable<int> source, int capacity)
                {
                    var list = new List<int>(capacity);
                    list.AddRange(source);
                    return list;
                }
            }

            public class C
            {
                public int M(IEnumerable<int> items)
                {
                    var total = 0;
                    foreach (var x in items.ToList(4))
                    {
                        total += x;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
