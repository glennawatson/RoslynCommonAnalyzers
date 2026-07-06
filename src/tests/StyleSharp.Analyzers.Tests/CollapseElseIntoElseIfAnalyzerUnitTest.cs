// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCollapseElse = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1465CollapseElseIntoElseIfAnalyzer,
    StyleSharp.Analyzers.Sst1465CollapseElseIntoElseIfCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1465 (collapse an else block that only wraps an if) and its fix.</summary>
public class CollapseElseIntoElseIfAnalyzerUnitTest
{
    /// <summary>Verifies an else block wrapping a bare if is reported and collapsed to else if.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseWrappingBareIfIsCollapsedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool first, bool second)
                                  {
                                      if (first)
                                      {
                                      }
                                      {|SST1465:else|}
                                      {
                                          if (second)
                                          {
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool first, bool second)
                                       {
                                           if (first)
                                           {
                                           }
                                           else if (second)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyCollapseElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies collapsing an else block wrapping an if/else keeps the inner chain untouched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseWrappingIfElseKeepsInnerChainAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool first, bool second)
                                  {
                                      if (first)
                                      {
                                          return 1;
                                      }
                                      {|SST1465:else|}
                                      {
                                          if (second)
                                          {
                                              return 2;
                                          }
                                          else
                                          {
                                              return 3;
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(bool first, bool second)
                                       {
                                           if (first)
                                           {
                                               return 1;
                                           }
                                           else if (second)
                                           {
                                               return 2;
                                           }
                                           else
                                           {
                                               return 3;
                                           }
                                       }
                                   }
                                   """;
        await VerifyCollapseElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies nested wrapped chains are reported at each level and collapse into one else-if chain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedElseChainsCollapseAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool first, bool second, bool third)
                                  {
                                      if (first)
                                      {
                                      }
                                      {|SST1465:else|}
                                      {
                                          if (second)
                                          {
                                          }
                                          {|SST1465:else|}
                                          {
                                              if (third)
                                              {
                                              }
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool first, bool second, bool third)
                                       {
                                           if (first)
                                           {
                                           }
                                           else if (second)
                                           {
                                           }
                                           else if (third)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyCollapseElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment inside the removed braces stays attached to the hoisted if.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockCommentInsideBracesIsKeptAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool first, bool second)
                                  {
                                      if (first)
                                      {
                                      }
                                      {|SST1465:else|}
                                      {
                                          /* keep */ if (second)
                                          {
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool first, bool second)
                                       {
                                           if (first)
                                           {
                                           }
                                           else /* keep */ if (second)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyCollapseElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an else block with more than one statement is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseBlockWithTwoStatementsIsCleanAsync()
        => await VerifyCollapseElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(bool first, bool second)
                {
                    if (first)
                    {
                        return 1;
                    }
                    else
                    {
                        var next = 2;
                        if (second)
                        {
                            return next;
                        }
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies an existing brace-free else-if chain is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BraceFreeElseIfIsCleanAsync()
        => await VerifyCollapseElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(bool first, bool second)
                {
                    if (first)
                    {
                    }
                    else if (second)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies an else block whose single statement is a while loop is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseBlockWrappingWhileIsCleanAsync()
        => await VerifyCollapseElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(bool first, bool second)
                {
                    if (first)
                    {
                    }
                    else
                    {
                        while (second)
                        {
                        }
                    }
                }
            }
            """);
}
