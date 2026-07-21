// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyGuard = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2273PreferGuardClauseAnalyzer,
    StyleSharp.Analyzers.Sst2273PreferGuardClauseCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2273 (prefer a guard clause over a trailing wrapping <c>if</c>). The rule is disabled by
/// default, so every test enables it through an <c>.editorconfig</c> severity entry.
/// </summary>
public class PreferGuardClauseAnalyzerUnitTest
{
    /// <summary>Verifies a trailing wrapping <c>if</c> in a void method becomes a <c>return</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidMethodTrailingIfBecomesReturnGuardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool ready)
                                  {
                                      {|SST2273:if|} (ready)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool ready)
                                       {
                                           if (!ready)
                                               return;
                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a trailing wrapping <c>if</c> in a loop body becomes a <c>continue</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoopTrailingIfBecomesContinueGuardAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M(List<int> items)
                                  {
                                      foreach (var i in items)
                                      {
                                          {|SST2273:if|} (i > 0)
                                          {
                                              System.Console.WriteLine(i);
                                              System.Console.WriteLine(i + 1);
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public void M(List<int> items)
                                       {
                                           foreach (var i in items)
                                           {
                                               if (!(i > 0))
                                                   continue;
                                               System.Console.WriteLine(i);
                                               System.Console.WriteLine(i + 1);
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an equality condition is flipped to its opposite operator, not wrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityConditionIsFlippedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int x)
                                  {
                                      {|SST2273:if|} (x == 0)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int x)
                                       {
                                           if (x != 0)
                                               return;
                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a leading <c>!</c> is stripped rather than doubled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedConditionUnwrapsAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool ready)
                                  {
                                      {|SST2273:if|} (!ready)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool ready)
                                       {
                                           if (ready)
                                               return;
                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies statements before the trailing <c>if</c> stay put ahead of the guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingStatementsAreKeptAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int x)
                                  {
                                      var y = x + 1;
                                      {|SST2273:if|} (x > 0)
                                      {
                                          System.Console.WriteLine(y);
                                          System.Console.WriteLine(x);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int x)
                                       {
                                           var y = x + 1;
                                           if (!(x > 0))
                                               return;
                                           System.Console.WriteLine(y);
                                           System.Console.WriteLine(x);
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an async <c>Task</c> method uses a <c>return</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncTaskMethodBecomesReturnGuardAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public sealed class C
                              {
                                  public async Task M(bool ready)
                                  {
                                      {|SST2273:if|} (ready)
                                      {
                                          await Task.Yield();
                                          System.Console.WriteLine("a");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public sealed class C
                                   {
                                       public async Task M(bool ready)
                                       {
                                           if (!ready)
                                               return;
                                           await Task.Yield();
                                           System.Console.WriteLine("a");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a value-free accessor body uses a <c>return</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetAccessorTrailingIfBecomesReturnGuardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private bool _enabled;

                                  public bool Enabled
                                  {
                                      set
                                      {
                                          {|SST2273:if|} (value)
                                          {
                                              _enabled = value;
                                              System.Console.WriteLine("set");
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private bool _enabled;

                                       public bool Enabled
                                       {
                                           set
                                           {
                                               if (!value)
                                                   return;
                                               _enabled = value;
                                               System.Console.WriteLine("set");
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constructor body uses a <c>return</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorTrailingIfBecomesReturnGuardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public C(bool ready)
                                  {
                                      {|SST2273:if|} (ready)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public C(bool ready)
                                       {
                                           if (!ready)
                                               return;
                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local function body uses a <c>return</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionTrailingIfBecomesReturnGuardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void Outer(bool ready)
                                  {
                                      Inner();

                                      void Inner()
                                      {
                                          {|SST2273:if|} (ready)
                                          {
                                              System.Console.WriteLine("a");
                                              System.Console.WriteLine("b");
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void Outer(bool ready)
                                       {
                                           Inner();

                                           void Inner()
                                           {
                                               if (!ready)
                                                   return;
                                               System.Console.WriteLine("a");
                                               System.Console.WriteLine("b");
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>for</c> loop body uses a <c>continue</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForLoopTrailingIfBecomesContinueGuardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int n)
                                  {
                                      for (int i = 0; i < n; i++)
                                      {
                                          {|SST2273:if|} (i == 3)
                                          {
                                              System.Console.WriteLine("a");
                                              System.Console.WriteLine("b");
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = 0; i < n; i++)
                                           {
                                               if (i != 3)
                                                   continue;
                                               System.Console.WriteLine("a");
                                               System.Console.WriteLine("b");
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inequality condition is flipped to equality.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InequalityConditionIsFlippedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int x)
                                  {
                                      {|SST2273:if|} (x != 0)
                                      {
                                          System.Console.WriteLine("a");
                                          System.Console.WriteLine("b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int x)
                                       {
                                           if (x == 0)
                                               return;
                                           System.Console.WriteLine("a");
                                           System.Console.WriteLine("b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an async iterator, where a bare <c>return;</c> is not valid, is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncIteratorMethodIsCleanAsync()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async IAsyncEnumerable<int> M(bool ready)
                {
                    await Task.Yield();
                    if (ready)
                    {
                        yield return 1;
                        yield return 2;
                    }
                }
            }
            """,
            optionLine: null);
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a <c>while</c> loop body uses a <c>continue</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WhileLoopTrailingIfBecomesContinueGuardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(int n)
                                  {
                                      while (n > 0)
                                      {
                                          n--;
                                          {|SST2273:if|} (n == 3)
                                          {
                                              System.Console.WriteLine("a");
                                              System.Console.WriteLine("b");
                                          }
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(int n)
                                       {
                                           while (n > 0)
                                           {
                                               n--;
                                               if (n != 3)
                                                   continue;
                                               System.Console.WriteLine("a");
                                               System.Console.WriteLine("b");
                                           }
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an async fully qualified <c>Task</c> return type uses a <c>return</c> guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedAsyncTaskBecomesReturnGuardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public async System.Threading.Tasks.Task M(bool ready)
                                  {
                                      {|SST2273:if|} (ready)
                                      {
                                          await System.Threading.Tasks.Task.Yield();
                                          System.Console.WriteLine("a");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public async System.Threading.Tasks.Task M(bool ready)
                                       {
                                           if (!ready)
                                               return;
                                           await System.Threading.Tasks.Task.Yield();
                                           System.Console.WriteLine("a");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies two trailing guards in one file are both reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoMethodsAreBothFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M1(bool a)
                                  {
                                      {|SST2273:if|} (a)
                                      {
                                          System.Console.WriteLine("1a");
                                          System.Console.WriteLine("1b");
                                      }
                                  }

                                  public void M2(bool b)
                                  {
                                      {|SST2273:if|} (b)
                                      {
                                          System.Console.WriteLine("2a");
                                          System.Console.WriteLine("2b");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M1(bool a)
                                       {
                                           if (!a)
                                               return;
                                           System.Console.WriteLine("1a");
                                           System.Console.WriteLine("1b");
                                       }

                                       public void M2(bool b)
                                       {
                                           if (!b)
                                               return;
                                           System.Console.WriteLine("2a");
                                           System.Console.WriteLine("2b");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a brace-less single statement fires and fixes when the threshold is lowered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracelessThenBranchFiresWithLoweredThresholdAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool ready)
                                  {
                                      {|SST2273:if|} (ready)
                                          System.Console.WriteLine("a");
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool ready)
                                       {
                                           if (!ready)
                                               return;
                                           System.Console.WriteLine("a");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, "stylesharp.SST2273.min_wrapped_statements = 1");
    }

    /// <summary>Verifies a nested embedded <c>if</c> — whose parent is another <c>if</c>, not a block — is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedEmbeddedIfIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public sealed class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                        if (b)
                        {
                            System.Console.WriteLine("a");
                            System.Console.WriteLine("b");
                        }
                }
            }
            """);

    /// <summary>Verifies a lowered <c>min_wrapped_statements</c> lets a single-statement body fire.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoweredThresholdFiresOnSingleStatementAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool ready)
                                  {
                                      {|SST2273:if|} (ready)
                                      {
                                          System.Console.WriteLine("a");
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool ready)
                                       {
                                           if (!ready)
                                               return;
                                           System.Console.WriteLine("a");
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource, "stylesharp.SST2273.min_wrapped_statements = 1");
    }

    /// <summary>Verifies an <c>if</c> with an <c>else</c> branch is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElseBranchIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public sealed class C
            {
                public void M(bool ready)
                {
                    if (ready)
                    {
                        System.Console.WriteLine("a");
                        System.Console.WriteLine("b");
                    }
                    else
                    {
                        System.Console.WriteLine("c");
                    }
                }
            }
            """);

    /// <summary>Verifies a trailing <c>if</c> wrapping a single statement is left alone by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleWrappedStatementIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public sealed class C
            {
                public void M(bool ready)
                {
                    if (ready)
                    {
                        System.Console.WriteLine("a");
                    }
                }
            }
            """);

    /// <summary>Verifies an <c>if</c> that is not the last statement is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StatementsAfterIfAreCleanAsync()
        => await VerifyCleanAsync(
            """
            public sealed class C
            {
                public void M(bool ready)
                {
                    if (ready)
                    {
                        System.Console.WriteLine("a");
                        System.Console.WriteLine("b");
                    }

                    System.Console.WriteLine("c");
                }
            }
            """);

    /// <summary>Verifies a block nested inside a <c>try</c> is not an exit boundary and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonBodyBlockIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public sealed class C
            {
                public void M(bool ready)
                {
                    try
                    {
                        if (ready)
                        {
                            System.Console.WriteLine("a");
                            System.Console.WriteLine("b");
                        }
                    }
                    finally
                    {
                        System.Console.WriteLine("done");
                    }
                }
            }
            """);

    /// <summary>Verifies an iterator method, where a bare <c>return;</c> is not valid, is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IteratorMethodIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public IEnumerable<int> M(bool ready)
                {
                    if (ready)
                    {
                        yield return 1;
                        yield return 2;
                    }
                }
            }
            """);

    /// <summary>Verifies a raised <c>min_wrapped_statements</c> keeps a two-statement body silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RaisedThresholdKeepsTwoStatementBodyCleanAsync()
        => await VerifyCleanAsync(
            """
            public sealed class C
            {
                public void M(bool ready)
                {
                    if (ready)
                    {
                        System.Console.WriteLine("a");
                        System.Console.WriteLine("b");
                    }
                }
            }
            """,
            "stylesharp.SST2273.min_wrapped_statements = 3");

    /// <summary>Runs a code-fix verification with the disabled rule enabled.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="optionLine">An optional extra <c>.editorconfig</c> option line.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? optionLine = null)
    {
        var test = CreateTest(source, optionLine);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="optionLine">An optional extra <c>.editorconfig</c> option line.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? optionLine = null)
    {
        var test = CreateTest(source, optionLine);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2273 enabled and any extra option applied.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="optionLine">An optional extra <c>.editorconfig</c> option line.</param>
    /// <returns>The configured test.</returns>
    private static VerifyGuard.Test CreateTest(string source, string? optionLine)
    {
        var test = new VerifyGuard.Test
        {
            TestCode = source,
        };

        var config = """
                     root = true

                     [*.cs]
                     dotnet_diagnostic.SST2273.severity = warning

                     """;
        if (optionLine is not null)
        {
            config += optionLine + "\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
