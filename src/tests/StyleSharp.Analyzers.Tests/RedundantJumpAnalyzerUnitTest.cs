// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantJump = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.RedundantJumpCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1174 (redundant jump statements) and its fix.</summary>
public class RedundantJumpAnalyzerUnitTest
{
    /// <summary>Verifies a trailing <c>return;</c> in a void method is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingReturnRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(int x)
                                  {
                                      System.Console.WriteLine(x);
                                      {|SST1174:return;|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(int x)
                                       {
                                           System.Console.WriteLine(x);
                                       }
                                   }
                                   """;
        await VerifyRedundantJump.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a trailing <c>continue;</c> in a loop body is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingContinueRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(int n)
                                  {
                                      for (var i = 0; i < n; i++)
                                      {
                                          System.Console.WriteLine(i);
                                          {|SST1174:continue;|}
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(int n)
                                       {
                                           for (var i = 0; i < n; i++)
                                           {
                                               System.Console.WriteLine(i);
                                           }
                                       }
                                   }
                                   """;
        await VerifyRedundantJump.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every trailing redundant jump across a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void A(int x)
                                  {
                                      System.Console.WriteLine(x);
                                      {|SST1174:return;|}
                                  }

                                  public void B(int y)
                                  {
                                      System.Console.WriteLine(y);
                                      {|SST1174:return;|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void A(int x)
                                       {
                                           System.Console.WriteLine(x);
                                       }

                                       public void B(int y)
                                       {
                                           System.Console.WriteLine(y);
                                       }
                                   }
                                   """;
        await VerifyRedundantJump.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>return;</c> that is not the tail of the method body is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTrailingReturnIsCleanAsync()
        => await VerifyRedundantJump.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool flag, int x)
                {
                    if (flag)
                    {
                        return;
                    }

                    System.Console.WriteLine(x);
                }
            }
            """);

    /// <summary>Verifies a <c>return;</c> with a value and a trailing <c>continue;</c> inside a nested block are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueReturnAndNestedContinueAreCleanAsync()
        => await VerifyRedundantJump.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                        if (i > 0)
                        {
                            continue;
                        }
                    }

                    return n;
                }
            }
            """);
}
