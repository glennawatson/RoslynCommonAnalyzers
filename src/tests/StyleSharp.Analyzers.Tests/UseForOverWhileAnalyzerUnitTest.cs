// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUseForOverWhile = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2287UseForOverWhileAnalyzer,
    StyleSharp.Analyzers.Sst2287UseForOverWhileCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2287UseForOverWhileAnalyzer"/> and its code fix (SST2287).</summary>
public class UseForOverWhileAnalyzerUnitTest
{
    /// <summary>Verifies a counter-owning while loop is reported and gathered into a for header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterOwningWhileIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          System.Console.WriteLine(i);
                                          i++;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = 0; i < n; i++)
                                           {
                                               System.Console.WriteLine(i);
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a compound-assignment step is gathered into the header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundAssignmentStepIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          System.Console.WriteLine(i);
                                          i += 2;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = 0; i < n; i += 2)
                                           {
                                               System.Console.WriteLine(i);
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a downward-counting loop with a prefix step is gathered into the header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrefixDecrementStepIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = n;
                                      {|SST2287:while (i > 0)|}
                                      {
                                          System.Console.WriteLine(i);
                                          --i;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = n; i > 0; --i)
                                           {
                                               System.Console.WriteLine(i);
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies statements around the loop are left where they are.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SurroundingStatementsAreKeptAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M(int n)
                                  {
                                      int total = 0;
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          total += i;
                                          i++;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M(int n)
                                       {
                                           int total = 0;
                                           for (int i = 0; i < n; i++)
                                           {
                                               total += i;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a counter read after the loop keeps the while form; a for header would scope it away.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterReadAfterLoopIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }

                    return i;
                }
            }
            """);

    /// <summary>Verifies a body holding a continue is left alone; a for header would run the step it skips.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodyWithContinueIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        if (i == 2)
                        {
                            i = 5;
                            continue;
                        }

                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a continue belonging to a nested loop does not block the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContinueInNestedLoopIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          foreach (var value in new int[0])
                                          {
                                              if (value == 0)
                                              {
                                                  continue;
                                              }
                                          }

                                          i++;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = 0; i < n; i++)
                                           {
                                               foreach (var value in new int[0])
                                               {
                                                   if (value == 0)
                                                   {
                                                       continue;
                                                   }
                                               }
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a loop whose condition ignores the declared local is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionWithoutCounterIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(bool flag)
                {
                    int i = 0;
                    while (flag)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a loop that does not end by stepping the counter is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoopWithoutTrailingStepIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        i++;
                        System.Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a loop with no declaration above it is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoopWithoutPrecedingDeclarationIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n, int i)
                {
                    System.Console.WriteLine(n);
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a declaration of two variables is left alone; a for header declares one group.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleDeclaratorsAreCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0, j = 1;
                    while (i < n)
                    {
                        System.Console.WriteLine(j);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies an uninitialized declaration is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UninitializedDeclarationIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i;
                    i = 0;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a single-statement body is left alone; there is no work left once the step moves out.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleStatementBodyIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a brace-less loop body is left alone; there is no trailing statement to lift.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmbeddedLoopBodyIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                        i++;
                }
            }
            """);

    /// <summary>Verifies a step whose amount reads the counter itself is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfReferencingStepIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 1;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i += i;
                    }
                }
            }
            """);

    /// <summary>Verifies a counter written after the loop keeps the while form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterWrittenAfterLoopIsCleanAsync()
        => await VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }

                    i = 0;
                    return n;
                }
            }
            """);
}
