// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRethrowOnlyCatch = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1470RemoveRethrowOnlyCatchAnalyzer,
    StyleSharp.Analyzers.Sst1470RemoveRethrowOnlyCatchCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1470 (trailing rethrow-only catch clause) and its fix.</summary>
public class RemoveRethrowOnlyCatchAnalyzerUnitTest
{
    /// <summary>Verifies a trailing rethrow-only clause after a real handler is reported and removed, keeping the handler.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingRethrowOnlyClauseIsRemovedAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          Console.WriteLine("work");
                                      }
                                      catch (InvalidOperationException)
                                      {
                                          Console.WriteLine("handled");
                                      }
                                      {|SST1470:catch|} (IOException)
                                      {
                                          throw;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;

                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                               Console.WriteLine("work");
                                           }
                                           catch (InvalidOperationException)
                                           {
                                               Console.WriteLine("handled");
                                           }
                                       }
                                   }
                                   """;
        await VerifyRethrowOnlyCatch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a trailing rethrow-only clause with a declared variable is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingClauseWithDeclaredVariableIsRemovedAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          Console.WriteLine("work");
                                      }
                                      catch (IOException)
                                      {
                                          Console.WriteLine("handled");
                                      }
                                      {|SST1470:catch|} (Exception ex)
                                      {
                                          throw;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;

                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                               Console.WriteLine("work");
                                           }
                                           catch (IOException)
                                           {
                                               Console.WriteLine("handled");
                                           }
                                       }
                                   }
                                   """;
        await VerifyRethrowOnlyCatch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a sole rethrow-only clause with a finally clause is removed while the try/finally is kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SoleClauseWithFinallyKeepsTryFinallyAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          Console.WriteLine("work");
                                      }
                                      {|SST1470:catch|}
                                      {
                                          throw;
                                      }
                                      finally
                                      {
                                          Console.WriteLine("cleanup");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                               Console.WriteLine("work");
                                           }
                                           finally
                                           {
                                               Console.WriteLine("cleanup");
                                           }
                                       }
                                   }
                                   """;
        await VerifyRethrowOnlyCatch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a sole rethrow-only clause without a finally unwraps the try, preserving the block's statements.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SoleClauseWithoutFinallyUnwrapsTryAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          Console.WriteLine("first");
                                          Console.WriteLine("second");
                                      }
                                      {|SST1470:catch|}
                                      {
                                          throw;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           Console.WriteLine("first");
                                           Console.WriteLine("second");
                                       }
                                   }
                                   """;
        await VerifyRethrowOnlyCatch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix unwraps every reported bare try/catch in a Fix All pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllUnwrapsEveryReportedTryAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void First()
                                  {
                                      try
                                      {
                                          Console.WriteLine("one");
                                      }
                                      {|SST1470:catch|}
                                      {
                                          throw;
                                      }
                                  }

                                  public void Second()
                                  {
                                      try
                                      {
                                          Console.WriteLine("two");
                                      }
                                      {|SST1470:catch|}
                                      {
                                          throw;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public void First()
                                       {
                                           Console.WriteLine("one");
                                       }

                                       public void Second()
                                       {
                                           Console.WriteLine("two");
                                       }
                                   }
                                   """;
        await VerifyRethrowOnlyCatch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a catch clause that does work before rethrowing is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClauseWithWorkBeforeRethrowIsCleanAsync()
        => await VerifyRethrowOnlyCatch.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    try
                    {
                        Console.WriteLine("work");
                    }
                    catch
                    {
                        Console.WriteLine("log");
                        throw;
                    }
                }
            }
            """);

    /// <summary>Verifies a rethrow-only clause with a when filter is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FilteredRethrowOnlyClauseIsCleanAsync()
        => await VerifyRethrowOnlyCatch.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    try
                    {
                        Console.WriteLine("work");
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Length > 0)
                    {
                        throw;
                    }
                }
            }
            """);

    /// <summary>Verifies a clause that rethrows the caught variable with 'throw ex;' is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RethrowWithExpressionIsCleanAsync()
        => await VerifyRethrowOnlyCatch.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    try
                    {
                        Console.WriteLine("work");
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw ex;
                    }
                }
            }
            """);

    /// <summary>Verifies a rethrow-only clause followed by another catch clause is clean, since it is not last.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RethrowOnlyClauseFollowedByAnotherClauseIsCleanAsync()
        => await VerifyRethrowOnlyCatch.VerifyAnalyzerAsync(
            """
            using System;
            using System.IO;

            public sealed class C
            {
                public void M()
                {
                    try
                    {
                        Console.WriteLine("work");
                    }
                    catch (IOException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("handled");
                    }
                }
            }
            """);
}
