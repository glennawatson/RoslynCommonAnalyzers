// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRequireBraces = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1503RequireBracesAnalyzer,
    StyleSharp.Analyzers.Sst1503RequireBracesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the require-braces rule (SST1503).</summary>
public class LayoutRequireBracesUnitTest
{
    /// <summary>Verifies a single-line unbraced child is reported (SST1503) and wrapped in braces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnbracedChildWrappedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int M(bool x)
                                  {
                                      {|SST1503:if|} (x) return 1;
                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int M(bool x)
                                       {
                                           if (x)
                                           {
                                               return 1;
                                           }
                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyRequireBraces.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All wraps every unbraced child in the document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private int M(bool x)
                                  {
                                      {|SST1503:if|} (x) return 1;
                                      {|SST1503:if|} (!x) return 2;
                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int M(bool x)
                                       {
                                           if (x)
                                           {
                                               return 1;
                                           }
                                           if (!x)
                                           {
                                               return 2;
                                           }
                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyRequireBraces.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a braced child is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracedChildIsCleanAsync()
        => await VerifyRequireBraces.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int M(bool x)
                {
                    if (x)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """);
}
