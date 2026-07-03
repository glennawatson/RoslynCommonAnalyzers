// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1305NoConcurrentSnapshotEnumerationAnalyzer,
    PerformanceSharp.Analyzers.Psh1305NoConcurrentSnapshotEnumerationCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1305NoConcurrentSnapshotEnumerationAnalyzer"/> (PSH1305 concurrent snapshot enumeration).</summary>
public class NoConcurrentSnapshotEnumerationAnalyzerUnitTest
{
    /// <summary>Verifies a foreach over Keys is flagged and deconstructed by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeysEnumerationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Concurrent;

                              public class C
                              {
                                  public int M(ConcurrentDictionary<string, int> map)
                                  {
                                      var total = 0;
                                      foreach (var key in {|PSH1305:map.Keys|})
                                      {
                                          total += key.Length;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Concurrent;

                                   public class C
                                   {
                                       public int M(ConcurrentDictionary<string, int> map)
                                       {
                                           var total = 0;
                                           foreach (var (key, _) in map)
                                           {
                                               total += key.Length;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a foreach over Values is flagged and deconstructed by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValuesEnumerationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Concurrent;

                              public class C
                              {
                                  public int M(ConcurrentDictionary<string, int> map)
                                  {
                                      var total = 0;
                                      foreach (var value in {|PSH1305:map.Values|})
                                      {
                                          total += value;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Concurrent;

                                   public class C
                                   {
                                       public int M(ConcurrentDictionary<string, int> map)
                                       {
                                           var total = 0;
                                           foreach (var (_, value) in map)
                                           {
                                               total += value;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a regular dictionary's Keys view stays clean; it is not a locking snapshot.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegularDictionaryKeysAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public int M(Dictionary<string, int> map)
                {
                    var total = 0;
                    foreach (var key in map.Keys)
                    {
                        total += key.Length;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies a non-foreach Keys use stays clean; the rule only targets enumeration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEnumerationKeysUseIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Concurrent;

            public class C
            {
                public int M(ConcurrentDictionary<string, int> map) => map.Keys.Count;
            }
            """);

    /// <summary>Verifies enumerating the dictionary itself is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DirectEnumerationIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Concurrent;

            public class C
            {
                public int M(ConcurrentDictionary<string, int> map)
                {
                    var total = 0;
                    foreach (var pair in map)
                    {
                        total += pair.Value;
                    }

                    return total;
                }
            }
            """);
}
