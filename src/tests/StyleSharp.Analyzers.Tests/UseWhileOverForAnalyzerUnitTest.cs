// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyWhile = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2245UseWhileOverForAnalyzer,
    StyleSharp.Analyzers.Sst2245UseWhileOverForCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2245UseWhileOverForAnalyzer"/> and its code fix (SST2245).</summary>
public class UseWhileOverForAnalyzerUnitTest
{
    /// <summary>Verifies a condition-only loop is reported and rewritten as a while loop.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionOnlyLoopIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M()
                                  {
                                      var i = 0;
                                      {|SST2245:for|} (; i < 10; )
                                      {
                                          i++;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M()
                                       {
                                           var i = 0;
                                           while (i < 10)
                                           {
                                               i++;
                                           }
                                       }
                                   }
                                   """;
        await VerifyWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an embedded single-statement body is carried through the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmbeddedBodyIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M()
                                  {
                                      var i = 0;
                                      {|SST2245:for|} (; i < 10;) i++;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M()
                                       {
                                           var i = 0;
                                           while (i < 10) i++;
                                       }
                                   }
                                   """;
        await VerifyWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comment inside the body survives the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodyTriviaSurvivesTheFixAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M()
                                  {
                                      var i = 0;

                                      // Count up to ten.
                                      {|SST2245:for|} (; i < 10; )
                                      {
                                          // Step.
                                          i++;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M()
                                       {
                                           var i = 0;

                                           // Count up to ten.
                                           while (i < 10)
                                           {
                                               // Step.
                                               i++;
                                           }
                                       }
                                   }
                                   """;
        await VerifyWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the idiomatic infinite loop stays clean; it has no condition to move.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InfiniteLoopIsCleanAsync()
        => await VerifyWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    for (;;)
                    {
                        break;
                    }
                }
            }
            """);

    /// <summary>Verifies a loop with a declaration in its initializer stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclarationLoopIsCleanAsync()
        => await VerifyWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    for (var i = 0; i < 10; i++)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a loop that keeps its incrementor stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IncrementorLoopIsCleanAsync()
        => await VerifyWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    var i = 0;
                    for (; i < 10; i++)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a loop that keeps an initializer expression stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerExpressionLoopIsCleanAsync()
        => await VerifyWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    var i = 1;
                    for (i = 0; i < 10;)
                    {
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a loop declaring several variables stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleDeclarationLoopIsCleanAsync()
        => await VerifyWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M()
                {
                    for (int i = 0, j = 10; i < j; i++)
                    {
                    }
                }
            }
            """);
}
