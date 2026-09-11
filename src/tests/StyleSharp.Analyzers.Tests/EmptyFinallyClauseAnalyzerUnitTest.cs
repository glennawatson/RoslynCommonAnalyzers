// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyEmptyFinally = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2466EmptyFinallyClauseAnalyzer,
    StyleSharp.Analyzers.Sst2466EmptyFinallyClauseCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2466EmptyFinallyClauseAnalyzer"/> and its code fix (SST2466).</summary>
public class EmptyFinallyClauseAnalyzerUnitTest
{
    /// <summary>Verifies a try carrying a region is reported but keeps its empty clause.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Removing the clause takes the braces that hold it, and lifting the body takes the try block's
    /// braces — the <c>#endregion</c> is the leading trivia of one of them.
    /// </remarks>
    [Test]
    public async Task TryCarryingADirectiveKeepsItsClauseAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                              #region Work
                                          Console.WriteLine("a");
                              #endregion
                                      }
                                      catch (Exception)
                                      {
                                          Console.WriteLine("b");
                                      }
                                      {|SST2466:finally|}
                                      {
                                      }
                                  }
                              }
                              """;
        await VerifyEmptyFinally.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies an empty finally beside a catch is reported and the clause removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyFinallyBesideCatchIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          Console.WriteLine(1);
                                      }
                                      catch (InvalidOperationException)
                                      {
                                          Console.WriteLine(2);
                                      }
                                      {|SST2466:finally|}
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       public void M()
                                       {
                                           try
                                           {
                                               Console.WriteLine(1);
                                           }
                                           catch (InvalidOperationException)
                                           {
                                               Console.WriteLine(2);
                                           }
                                       }
                                   }
                                   """;
        await VerifyEmptyFinally.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a try guarding nothing collapses to its own statements.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryWithNoCatchCollapsesToItsBodyAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  public void M()
                                  {
                                      try
                                      {
                                          Console.WriteLine(1);
                                          Console.WriteLine(2);
                                      }
                                      {|SST2466:finally|}
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       public void M()
                                       {
                                           Console.WriteLine(1);
                                           Console.WriteLine(2);
                                       }
                                   }
                                   """;
        await VerifyEmptyFinally.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a finally that runs cleanup is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FinallyWithCleanupIsCleanAsync() =>
        VerifyEmptyFinally.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public void M()
                {
                    try
                    {
                        Console.WriteLine(1);
                    }
                    finally
                    {
                        Console.WriteLine(2);
                    }
                }
            }
            """);

    /// <summary>Verifies a finally holding only a comment is left alone; the comment may be the reason.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FinallyWithOnlyACommentIsCleanAsync() =>
        VerifyEmptyFinally.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public void M()
                {
                    try
                    {
                        Console.WriteLine(1);
                    }
                    finally
                    {
                        // Nothing to release: the handle is owned by the caller.
                    }
                }
            }
            """);
}
