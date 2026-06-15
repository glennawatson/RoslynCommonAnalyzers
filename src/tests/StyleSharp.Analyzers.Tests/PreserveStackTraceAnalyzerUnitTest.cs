// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRethrow = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExceptionHandlingAnalyzer,
    StyleSharp.Analyzers.PreserveStackTraceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1430 (rethrow that loses the stack trace) and its fix.</summary>
public class PreserveStackTraceAnalyzerUnitTest
{
    /// <summary>Verifies <c>throw ex;</c> on the caught variable is reported and replaced with <c>throw;</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RethrowCaughtVariableFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                      }
                                      catch (Exception ex)
                                      {
                                          {|SST1430:throw ex;|}
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                           }
                                           catch (Exception ex)
                                           {
                                               throw;
                                           }
                                       }
                                   }
                                   """;
        await VerifyRethrow.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All replaces every stack-trace-losing rethrow in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                      }
                                      catch (Exception ex)
                                      {
                                          {|SST1430:throw ex;|}
                                      }
                                  }

                                  public void N()
                                  {
                                      try
                                      {
                                      }
                                      catch (Exception ex)
                                      {
                                          {|SST1430:throw ex;|}
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                           }
                                           catch (Exception ex)
                                           {
                                               throw;
                                           }
                                       }

                                       public void N()
                                       {
                                           try
                                           {
                                           }
                                           catch (Exception ex)
                                           {
                                               throw;
                                           }
                                       }
                                   }
                                   """;
        await VerifyRethrow.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a bare <c>throw;</c> and throwing a different exception are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareThrowAndNewExceptionAreCleanAsync()
        => await VerifyRethrow.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M()
                {
                    try
                    {
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("wrapped", ex);
                    }

                    try
                    {
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            """);
}
