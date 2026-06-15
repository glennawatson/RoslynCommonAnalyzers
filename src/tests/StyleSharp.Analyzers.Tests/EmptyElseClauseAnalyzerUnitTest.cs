// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyElse = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.EmptyElseClauseCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1180 (empty else clause) and its fix.</summary>
public class EmptyElseClauseAnalyzerUnitTest
{
    /// <summary>Verifies an <c>else { }</c> with an empty body is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyElseBlockRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool flag)
                                  {
                                      if (flag)
                                      {
                                          System.Console.WriteLine();
                                      }
                                      {|SST1180:else|}
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool flag)
                                       {
                                           if (flag)
                                           {
                                               System.Console.WriteLine();
                                           }
                                       }
                                   }
                                   """;
        await VerifyEmptyElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every empty else clause in a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void A(bool flag)
                                  {
                                      if (flag)
                                      {
                                          System.Console.WriteLine();
                                      }
                                      {|SST1180:else|}
                                      {
                                      }
                                  }

                                  public void B(bool flag)
                                  {
                                      if (flag)
                                      {
                                          System.Console.WriteLine();
                                      }
                                      {|SST1180:else|}
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void A(bool flag)
                                       {
                                           if (flag)
                                           {
                                               System.Console.WriteLine();
                                           }
                                       }

                                       public void B(bool flag)
                                       {
                                           if (flag)
                                           {
                                               System.Console.WriteLine();
                                           }
                                       }
                                   }
                                   """;
        await VerifyEmptyElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a non-empty <c>else</c> and an <c>else if</c> chain are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEmptyElseIsCleanAsync()
        => await VerifyEmptyElse.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(int value)
                {
                    if (value == 1)
                    {
                        System.Console.WriteLine();
                    }
                    else if (value == 2)
                    {
                    }
                    else
                    {
                        System.Console.WriteLine();
                    }
                }
            }
            """);
}
