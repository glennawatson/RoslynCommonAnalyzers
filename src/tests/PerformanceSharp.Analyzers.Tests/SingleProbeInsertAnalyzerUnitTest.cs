// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1115SingleProbeInsertAnalyzer,
    PerformanceSharp.Analyzers.Psh1115SingleProbeInsertCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1115SingleProbeInsertAnalyzer"/> (PSH1115 single-probe inserts).</summary>
public class SingleProbeInsertAnalyzerUnitTest
{
    /// <summary>Verifies a ContainsKey-guarded indexer write is flagged and becomes TryAdd.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedIndexerWriteIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key, int value)
                                  {
                                      {|PSH1115:if (!map.ContainsKey(key))
                                      {
                                          map[key] = value;
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public void M(Dictionary<string, int> map, string key, int value)
                                       {
                                           map.TryAdd(key, value);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a failed TryGetValue followed by a store is reported for the value-slot API.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetValueStoreIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> M(Dictionary<string, List<int>> map, string key)
                {
                    {|PSH1115:if (!map.TryGetValue(key, out var bucket))
                    {
                        bucket = new List<int>();
                        map[key] = bucket;
                    }|}

                    return bucket;
                }
            }
            """);

    /// <summary>Verifies a guard writing a different key stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentKeyIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(Dictionary<string, int> map, string key, string other, int value)
                {
                    if (!map.ContainsKey(key))
                    {
                        map[other] = value;
                    }
                }
            }
            """);

    /// <summary>Verifies a guard with an else branch stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardWithElseIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(Dictionary<string, int> map, string key, int value)
                {
                    if (!map.ContainsKey(key))
                    {
                        map[key] = value;
                    }
                    else
                    {
                        map[key] = value + 1;
                    }
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
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
