// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyExceptionFilter = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2009UseExceptionFilterAnalyzer,
    StyleSharp.Analyzers.Sst2009UseExceptionFilterCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2009 (hand-rolled exception filter) and its fix.</summary>
public class UseExceptionFilterAnalyzerUnitTest
{
    /// <summary>Verifies an else-branch rethrow moves the condition into the filter as-is.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormAElseRethrowMovesConditionIntoFilterAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool flag)
                                  {
                                      try
                                      {
                                          M(flag);
                                      }
                                      catch (System.Exception ex)
                                      {
                                          {|SST2009:if|} (flag)
                                          {
                                              System.Console.WriteLine(ex.Message);
                                          }
                                          else
                                          {
                                              throw;
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool flag)
                                       {
                                           try
                                           {
                                               M(flag);
                                           }
                                           catch (System.Exception ex) when (flag)
                                           {
                                               System.Console.WriteLine(ex.Message);
                                           }
                                       }
                                   }
                                   """;
        await VerifyExceptionFilter.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a then-branch rethrow negates the condition and keeps the else branch as the body.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormAThenRethrowNegatesConditionAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool flag)
                                  {
                                      try
                                      {
                                          M(flag);
                                      }
                                      catch (System.Exception ex)
                                      {
                                          {|SST2009:if|} (flag)
                                          {
                                              throw;
                                          }
                                          else
                                          {
                                              System.Console.WriteLine(ex.Message);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool flag)
                                       {
                                           try
                                           {
                                               M(flag);
                                           }
                                           catch (System.Exception ex) when (!(flag))
                                           {
                                               System.Console.WriteLine(ex.Message);
                                           }
                                       }
                                   }
                                   """;
        await VerifyExceptionFilter.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a leading rethrow guard negates the condition and keeps the remaining statements.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormBNegatesConditionAndKeepsRemainingStatementsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(bool flag)
                                  {
                                      try
                                      {
                                          M(flag);
                                      }
                                      catch (System.Exception ex)
                                      {
                                          {|SST2009:if|} (flag)
                                          {
                                              throw;
                                          }

                                          System.Console.WriteLine(ex.Message);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(bool flag)
                                       {
                                           try
                                           {
                                               M(flag);
                                           }
                                           catch (System.Exception ex) when (!(flag))
                                           {
                                               System.Console.WriteLine(ex.Message);
                                           }
                                       }
                                   }
                                   """;
        await VerifyExceptionFilter.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies negating a comparison inverts its operator instead of wrapping it in <c>!(...)</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonConditionInvertsOperatorAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          M();
                                      }
                                      catch (System.Exception ex)
                                      {
                                          {|SST2009:if|} (ex.HResult == 0)
                                          {
                                              throw;
                                          }
                                          else
                                          {
                                              System.Console.WriteLine(ex.Message);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                               M();
                                           }
                                           catch (System.Exception ex) when (ex.HResult != 0)
                                           {
                                               System.Console.WriteLine(ex.Message);
                                           }
                                       }
                                   }
                                   """;
        await VerifyExceptionFilter.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a relational guard in a leading rethrow inverts to the opposite operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RelationalComparisonInvertsOperatorAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          M();
                                      }
                                      catch (System.Exception ex)
                                      {
                                          {|SST2009:if|} (ex.HResult < 0) throw;
                                          System.Console.WriteLine(ex.Message);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                               M();
                                           }
                                           catch (System.Exception ex) when (ex.HResult >= 0)
                                           {
                                               System.Console.WriteLine(ex.Message);
                                           }
                                       }
                                   }
                                   """;
        await VerifyExceptionFilter.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every hand-rolled filter in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void A(bool flag)
                                  {
                                      try
                                      {
                                          A(flag);
                                      }
                                      catch (System.Exception ex)
                                      {
                                          {|SST2009:if|} (flag)
                                          {
                                              System.Console.WriteLine(ex.Message);
                                          }
                                          else
                                          {
                                              throw;
                                          }
                                      }
                                  }

                                  public void B(bool flag)
                                  {
                                      try
                                      {
                                          B(flag);
                                      }
                                      catch (System.Exception ex)
                                      {
                                          {|SST2009:if|} (flag)
                                          {
                                              throw;
                                          }

                                          System.Console.WriteLine(ex.Message);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void A(bool flag)
                                       {
                                           try
                                           {
                                               A(flag);
                                           }
                                           catch (System.Exception ex) when (flag)
                                           {
                                               System.Console.WriteLine(ex.Message);
                                           }
                                       }

                                       public void B(bool flag)
                                       {
                                           try
                                           {
                                               B(flag);
                                           }
                                           catch (System.Exception ex) when (!(flag))
                                           {
                                               System.Console.WriteLine(ex.Message);
                                           }
                                       }
                                   }
                                   """;
        await VerifyExceptionFilter.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a condition that invokes a method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvocationConditionIsCleanAsync()
        => await VerifyExceptionFilter.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    try
                    {
                        M();
                    }
                    catch (System.Exception ex)
                    {
                        if (ShouldKeep())
                        {
                            System.Console.WriteLine(ex.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                private static bool ShouldKeep() => true;
            }
            """);

    /// <summary>Verifies a catch that already has a filter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExistingFilterIsCleanAsync()
        => await VerifyExceptionFilter.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool flag)
                {
                    try
                    {
                        M(flag);
                    }
                    catch (System.Exception ex) when (ex.HResult != 0)
                    {
                        if (flag)
                        {
                            throw;
                        }

                        System.Console.WriteLine(ex.Message);
                    }
                }
            }
            """);

    /// <summary>Verifies a branch that throws an expression instead of rethrowing is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowWithExpressionIsCleanAsync()
        => await VerifyExceptionFilter.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool flag)
                {
                    try
                    {
                        M(flag);
                    }
                    catch (System.Exception ex)
                    {
                        if (flag)
                        {
                            System.Console.WriteLine(ex.Message);
                        }
                        else
                        {
                            throw ex;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies an <c>if</c> that is not the catch block's first statement is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfNotFirstStatementIsCleanAsync()
        => await VerifyExceptionFilter.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M(bool flag)
                {
                    try
                    {
                        M(flag);
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine(ex.Message);
                        if (flag)
                        {
                            throw;
                        }
                    }
                }
            }
            """);
}
