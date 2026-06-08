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
