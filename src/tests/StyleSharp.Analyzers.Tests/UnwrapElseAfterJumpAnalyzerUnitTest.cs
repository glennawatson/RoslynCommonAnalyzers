// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnwrapElse = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1464UnwrapElseAfterJumpAnalyzer,
    StyleSharp.Analyzers.Sst1464UnwrapElseAfterJumpCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1464 (unwrap else after a branch that does not fall through) and its fix.</summary>
public class UnwrapElseAfterJumpAnalyzerUnitTest
{
    /// <summary>Verifies an else after an if branch ending in return is reported and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfReturnElseBlockIsUnwrappedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int value)
                                  {
                                      if (value > 0)
                                      {
                                          return 1;
                                      }
                                      {|SST1464:else|}
                                      {
                                          return 2;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int value)
                                       {
                                           if (value > 0)
                                           {
                                               return 1;
                                           }
                                           return 2;
                                       }
                                   }
                                   """;
        await VerifyUnwrapElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an else after an if branch ending in throw is reported and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfThrowElseBlockIsUnwrappedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int value)
                                  {
                                      if (value < 0)
                                      {
                                          throw new System.ArgumentOutOfRangeException(nameof(value));
                                      }
                                      {|SST1464:else|}
                                      {
                                          return value * 2;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int value)
                                       {
                                           if (value < 0)
                                           {
                                               throw new System.ArgumentOutOfRangeException(nameof(value));
                                           }
                                           return value * 2;
                                       }
                                   }
                                   """;
        await VerifyUnwrapElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an else after a break guard inside a loop is reported and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BreakGuardInsideLoopIsUnwrappedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int limit)
                                  {
                                      var total = 0;
                                      while (true)
                                      {
                                          if (total >= limit)
                                          {
                                              break;
                                          }
                                          {|SST1464:else|}
                                          {
                                              total++;
                                          }
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int limit)
                                       {
                                           var total = 0;
                                           while (true)
                                           {
                                               if (total >= limit)
                                               {
                                                   break;
                                               }
                                               total++;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyUnwrapElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an else after a continue guard inside a loop is reported and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContinueGuardInsideLoopIsUnwrappedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int[] values)
                                  {
                                      var total = 0;
                                      foreach (var value in values)
                                      {
                                          if (value < 0)
                                          {
                                              continue;
                                          }
                                          {|SST1464:else|}
                                          {
                                              total += value;
                                          }
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int[] values)
                                       {
                                           var total = 0;
                                           foreach (var value in values)
                                           {
                                               if (value < 0)
                                               {
                                                   continue;
                                               }
                                               total += value;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyUnwrapElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an else-if is hoisted as a whole if statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseIfIsHoistedAsWholeIfAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int value)
                                  {
                                      if (value > 0)
                                      {
                                          return 1;
                                      }
                                      {|SST1464:else|} if (value < 0)
                                      {
                                          return 2;
                                      }

                                      return 3;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int value)
                                       {
                                           if (value > 0)
                                           {
                                               return 1;
                                           }
                                           if (value < 0)
                                           {
                                               return 2;
                                           }

                                           return 3;
                                       }
                                   }
                                   """;
        await VerifyUnwrapElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an if branch that falls through keeps its else clause.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonJumpIfBranchIsCleanAsync()
        => await VerifyUnwrapElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    var total = 0;
                    if (value > 0)
                    {
                        total = 1;
                    }
                    else
                    {
                        total = 2;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an empty if branch keeps its else clause.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyIfBranchIsCleanAsync()
        => await VerifyUnwrapElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    var total = 0;
                    if (value > 0)
                    {
                    }
                    else
                    {
                        total = 2;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies an if statement without an else clause is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfWithoutElseIsCleanAsync()
        => await VerifyUnwrapElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    if (value > 0)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies the diagnostic is still reported when the fix is withheld because the else declares a local and statements follow the if.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseWithLocalsAndFollowingStatementsReportsWithoutFixAsync()
        => await VerifyUnwrapElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    var total = 3;
                    if (value > 0)
                    {
                        return 1;
                    }
                    {|SST1464:else|}
                    {
                        var local = value + 1;
                        total += local;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies the diagnostic is still reported when the fix is withheld because the if statement is not directly inside a block.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseOfUnbracedNestedIfReportsWithoutFixAsync()
        => await VerifyUnwrapElse.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(bool first, bool second)
                {
                    var value = 0;
                    if (first)
                        if (second)
                        {
                            return value;
                        }
                        {|SST1464:else|}
                        {
                            value = 1;
                        }

                    return value;
                }
            }
            """);

    /// <summary>Verifies Fix All unwraps nested else clauses in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllUnwrapsNestedElseClausesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int value)
                                  {
                                      if (value > 10)
                                      {
                                          return 3;
                                      }
                                      {|SST1464:else|}
                                      {
                                          if (value > 5)
                                          {
                                              return 2;
                                          }
                                          {|SST1464:else|}
                                          {
                                              return 1;
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(int value)
                                       {
                                           if (value > 10)
                                           {
                                               return 3;
                                           }
                                           if (value > 5)
                                           {
                                               return 2;
                                           }
                                           return 1;
                                       }
                                   }
                                   """;
        await VerifyUnwrapElse.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the trailing else of a chain whose first arm falls through is not reported.</summary>
    /// <remarks>
    /// The else belongs to an if that is itself an else clause, so its statements have nowhere to hoist
    /// to: moving the <c>continue</c> out would put it on the path the first arm takes as well, and the
    /// loop would continue where it used to fall through. The else is carrying the chain, not nesting it.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingElseOfChainWhoseFirstArmFallsThroughIsNotReportedAsync()
        => await VerifyUnwrapElse.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    var total = 0;
                    foreach (var value in values)
                    {
                        if (value > 10)
                        {
                            total += value;
                        }
                        else if (value < 0)
                        {
                            return -1;
                        }
                        else
                        {
                            continue;
                        }

                        total++;
                    }

                    return total;
                }
            }
            """);
}
