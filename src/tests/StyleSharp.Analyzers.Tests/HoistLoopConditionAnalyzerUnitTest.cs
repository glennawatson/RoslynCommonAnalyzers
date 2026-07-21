// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyHoist = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2263HoistLoopConditionAnalyzer,
    StyleSharp.Analyzers.Sst2263HoistLoopConditionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2263HoistLoopConditionAnalyzer"/> and its code fix (SST2263).</summary>
public class HoistLoopConditionAnalyzerUnitTest
{
    /// <summary>Verifies the if-then-else-break shape hoists its condition into a while header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfElseBreakIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              internal class C
                              {
                                  public void M(Queue<int> queue)
                                  {
                                      {|SST2263:while|} (true)
                                      {
                                          if (queue.Count > 0)
                                          {
                                              queue.Dequeue();
                                          }
                                          else
                                          {
                                              break;
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   internal class C
                                   {
                                       public void M(Queue<int> queue)
                                       {
                                           while (queue.Count > 0)
                                           {
                                               queue.Dequeue();
                                           }
                                       }
                                   }
                                   """;
        await VerifyHoist.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the same shape on a <c>for (;;)</c> loop hoists into a while header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEverIfElseBreakIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              internal class C
                              {
                                  public void M(Queue<int> queue)
                                  {
                                      {|SST2263:for|} (;;)
                                      {
                                          if (queue.Count > 0)
                                          {
                                              queue.Dequeue();
                                          }
                                          else
                                          {
                                              break;
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   internal class C
                                   {
                                       public void M(Queue<int> queue)
                                       {
                                           while (queue.Count > 0)
                                           {
                                               queue.Dequeue();
                                           }
                                       }
                                   }
                                   """;
        await VerifyHoist.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negated guard-then-break shape unwraps the negation into the header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedGuardThenBreakIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(bool ready)
                                  {
                                      {|SST2263:while|} (true)
                                      {
                                          if (!ready)
                                          {
                                              break;
                                          }

                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(bool ready)
                                       {
                                           while (ready)
                                           {

                                               System.Console.WriteLine();
                                           }
                                       }
                                   }
                                   """;
        await VerifyHoist.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a plain guard-then-break shape negates the guard into the header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainGuardThenBreakIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(bool done)
                                  {
                                      {|SST2263:while|} (true)
                                      {
                                          if (done)
                                          {
                                              break;
                                          }

                                          System.Console.WriteLine();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(bool done)
                                       {
                                           while (!done)
                                           {

                                               System.Console.WriteLine();
                                           }
                                       }
                                   }
                                   """;
        await VerifyHoist.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an infinite loop whose guarded body is empty is left to the rules that own that shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyGuardedBodyIsCleanAsync()
        => await VerifyHoist.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(bool cond)
                {
                    while (true)
                    {
                        if (cond)
                        {
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies an infinite loop that is not a condition-hoist shape is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainInfiniteLoopIsCleanAsync()
        => await VerifyHoist.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    while (true)
                    {
                        System.Console.WriteLine();
                        break;
                    }
                }
            }
            """);
}
