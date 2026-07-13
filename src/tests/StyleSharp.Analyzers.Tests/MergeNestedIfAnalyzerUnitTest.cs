// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMerge = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2013MergeNestedIfAnalyzer,
    StyleSharp.Analyzers.Sst2013MergeNestedIfCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2013 (merge an if that only wraps another if) and its fix.</summary>
public class MergeNestedIfAnalyzerUnitTest
{
    /// <summary>Verifies a braced nested pair is reported and merged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracedNestedIfIsMergedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool a, bool b)
                                  {
                                      {|SST2013:if|} (a)
                                      {
                                          if (b)
                                          {
                                              System.Console.WriteLine(1);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool a, bool b)
                                       {
                                           if (a && b)
                                           {
                                               System.Console.WriteLine(1);
                                           }
                                       }
                                   }
                                   """;

        await VerifyMerge.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a bare nested pair with no braces is reported and merged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareNestedIfIsMergedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool a, bool b)
                                  {
                                      {|SST2013:if|} (a)
                                          if (b)
                                              System.Console.WriteLine(1);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool a, bool b)
                                       {
                                           if (a && b)
                                               System.Console.WriteLine(1);
                                       }
                                   }
                                   """;

        await VerifyMerge.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a looser-binding condition keeps its grouping through the merge.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LooseConditionsAreParenthesizedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool a, bool b, bool c, bool d)
                                  {
                                      {|SST2013:if|} (a || b)
                                      {
                                          if (c || d)
                                          {
                                              System.Console.WriteLine(1);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool a, bool b, bool c, bool d)
                                       {
                                           if ((a || b) && (c || d))
                                           {
                                               System.Console.WriteLine(1);
                                           }
                                       }
                                   }
                                   """;

        await VerifyMerge.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a tighter-binding condition is left exactly as it was written.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TightConditionsKeepTheirShapeAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(object value, int count)
                                  {
                                      {|SST2013:if|} (value is string text && text.Length > 0)
                                      {
                                          if (count > 1 && count < 10)
                                          {
                                              System.Console.WriteLine(text);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(object value, int count)
                                       {
                                           if (value is string text && text.Length > 0 && count > 1 && count < 10)
                                           {
                                               System.Console.WriteLine(text);
                                           }
                                       }
                                   }
                                   """;

        await VerifyMerge.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment in the discarded scaffolding is carried over rather than deleted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CommentsSurviveTheMergeAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool a, bool b)
                                  {
                                      {|SST2013:if|} (a)
                                      {
                                          // b is the expensive one, so it is tested second.
                                          if (b)
                                          {
                                              System.Console.WriteLine(1);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool a, bool b)
                                       {
                                           // b is the expensive one, so it is tested second.
                                           if (a && b)
                                           {
                                               System.Console.WriteLine(1);
                                           }
                                       }
                                   }
                                   """;

        await VerifyMerge.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an else on either if stops the merge: a merged condition cannot tell the branches apart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EitherElseStopsTheMergeAsync()
        => await VerifyMerge.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void OuterElse(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                            System.Console.WriteLine(1);
                        }
                    }
                    else
                    {
                        System.Console.WriteLine(2);
                    }
                }

                public void InnerElse(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                            System.Console.WriteLine(1);
                        }
                        else
                        {
                            System.Console.WriteLine(2);
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies an outer if that does more than wrap the inner one is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OuterIfWithOtherStatementsIsCleanAsync()
        => await VerifyMerge.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                    {
                        System.Console.WriteLine(0);
                        if (b)
                        {
                            System.Console.WriteLine(1);
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies the merged clause of an else-if chain reports and merges like any other if.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseIfClauseIsMergedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool a, bool b, bool c)
                                  {
                                      if (a)
                                      {
                                          System.Console.WriteLine(0);
                                      }
                                      else {|SST2013:if|} (b)
                                      {
                                          if (c)
                                          {
                                              System.Console.WriteLine(1);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool a, bool b, bool c)
                                       {
                                           if (a)
                                           {
                                               System.Console.WriteLine(0);
                                           }
                                           else if (b && c)
                                           {
                                               System.Console.WriteLine(1);
                                           }
                                       }
                                   }
                                   """;

        await VerifyMerge.VerifyCodeFixAsync(Source, FixedSource);
    }
}
