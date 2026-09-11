// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOrPattern = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1144PreferOrPatternAnalyzer,
    StyleSharp.Analyzers.Sst1144PreferOrPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1144 (combine case labels with an or-pattern) and its fix.</summary>
public class PreferOrPatternAnalyzerUnitTest
{
    /// <summary>Verifies stacked case labels are reported and merged into a single or-pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StackedLabelsMergedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int x)
                                  {
                                      switch (x)
                                      {
                                          {|SST1144:case 1:|}
                                          case 2:
                                              return 0;
                                          default:
                                              return 1;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int x)
                                       {
                                           switch (x)
                                           {
                                               case 1 or 2:
                                                   return 0;
                                               default:
                                                   return 1;
                                           }
                                       }
                                   }
                                   """;
        await VerifyOrPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All merges stacked case labels in every switch in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int A(int x)
                                  {
                                      switch (x)
                                      {
                                          {|SST1144:case 1:|}
                                          case 2:
                                              return 0;
                                          default:
                                              return 1;
                                      }
                                  }

                                  public int B(int x)
                                  {
                                      switch (x)
                                      {
                                          {|SST1144:case 3:|}
                                          case 4:
                                              return 0;
                                          default:
                                              return 1;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int A(int x)
                                       {
                                           switch (x)
                                           {
                                               case 1 or 2:
                                                   return 0;
                                               default:
                                                   return 1;
                                           }
                                       }

                                       public int B(int x)
                                       {
                                           switch (x)
                                           {
                                               case 3 or 4:
                                                   return 0;
                                               default:
                                                   return 1;
                                           }
                                       }
                                   }
                                   """;
        await VerifyOrPattern.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies labels that would not fit on one line are left stacked.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Merging them writes a line past the layout ceiling, so the code that came out of the fix would draw
    /// SST1521 and satisfy neither rule.
    /// </remarks>
    [Test]
    public async Task LabelsThatWouldOverrunTheLineAreCleanAsync()
        => await VerifyOrPattern.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(object value)
                {
                    switch (value)
                    {
                        case System.Collections.Generic.List<int>:
                        case System.Collections.Generic.HashSet<int>:
                        case System.Collections.Generic.Queue<int>:
                            return 0;
                        default:
                            return 1;
                    }
                }
            }
            """);

    /// <summary>Verifies a single-label section and a guarded label are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLabelAndGuardedAreCleanAsync()
        => await VerifyOrPattern.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            return 0;
                        case int n when n > 5:
                        case 2:
                            return 1;
                        default:
                            return 2;
                    }
                }
            }
            """);
}
