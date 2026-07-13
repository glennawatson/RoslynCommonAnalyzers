// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyUnusedLocal = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1497UnusedLocalAnalyzer,
    StyleSharp.Analyzers.Sst1497UnusedLocalCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1497 (a local is declared and never read) and its fix.</summary>
public class UnusedLocalAnalyzerUnitTest
{
    /// <summary>Verifies a local with a side-effect-free initializer is removed outright.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreadLocalIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Run(int value)
                                  {
                                      int {|SST1497:unused|} = value + 1;
                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Run(int value)
                                       {
                                           return value;
                                       }
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local whose value comes from a call keeps the call and loses only the variable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectingInitializerIsKeptAsStatementAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Compute() => 1;

                                  public void Run()
                                  {
                                      var {|SST1497:result|} = Compute();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Compute() => 1;

                                       public void Run()
                                       {
                                           Compute();
                                       }
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression that cannot stand as a statement is kept as a discard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionThatIsNotAStatementBecomesADiscardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Run(int[] values, int index)
                                  {
                                      var {|SST1497:element|} = values[index];
                                      return index;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Run(int[] values, int index)
                                       {
                                           _ = values[index];
                                           return index;
                                       }
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local that is written but never read is unused, and its dead writes go with it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignedButNeverReadLocalIsRemovedWithItsWritesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Compute() => 1;

                                  public void Run(bool flag)
                                  {
                                      int {|SST1497:state|} = 0;
                                      if (flag)
                                      {
                                          state = 1;
                                      }

                                      state = Compute();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Compute() => 1;

                                       public void Run(bool flag)
                                       {
                                           if (flag)
                                           {
                                           }

                                           Compute();
                                       }
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local read even once is used, wherever the read happens.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadLocalIsCleanAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public int Run(int value)
                {
                    var doubled = value * 2;
                    var total = 0;
                    total += doubled;
                    var counter = 0;
                    counter++;
                    Console.WriteLine(total + counter);
                    return doubled;
                }
            }
            """);

    /// <summary>Verifies a local read only inside a lambda or a local function is still read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalCapturedByANestedFunctionIsCleanAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public Func<int> Run(int value)
                {
                    var captured = value;
                    var byLocalFunction = value + 1;

                    int Inner() => byLocalFunction;

                    Console.WriteLine(Inner());
                    return () => captured;
                }
            }
            """);

    /// <summary>Verifies a discard, a using declaration, a foreach variable and a pattern variable are all left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclarationsWhoseLifetimeIsThePointAreCleanAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.IO;

            public sealed class C
            {
                public void Run(List<int> values, object candidate)
                {
                    using var stream = new MemoryStream();
                    var _ = values.Count;

                    foreach (var value in values)
                    {
                    }

                    if (candidate is string text)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a ref local is left alone, since the alias it holds is the point of declaring it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefLocalIsCleanAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Run(int[] values)
                {
                    ref int slot = ref values[0];
                    slot = 1;
                }
            }
            """);

    /// <summary>Verifies an unread out variable becomes a discard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreadOutVariableBecomesADiscardAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool Run(string text) => int.TryParse(text, out var {|SST1497:value|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool Run(string text) => int.TryParse(text, out _);
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an out variable the caller goes on to read is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOutVariableIsCleanAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Run(string text)
                {
                    if (int.TryParse(text, out var value))
                    {
                        return value;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a deconstruction is not an out variable and is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeconstructionIsCleanAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Run((int Left, int Right) pair)
                {
                    var (left, right) = pair;
                    Console.WriteLine(left + right);
                }
            }
            """);

    /// <summary>Verifies only the unread variable of a multi-variable declaration is removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyTheUnreadVariableOfADeclarationIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Run()
                                  {
                                      int used = 1, {|SST1497:unused|} = 2;
                                      return used;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Run()
                                       {
                                           int used = 1;
                                           return used;
                                       }
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a lambda assigned to an unread local is removed, because writing one down runs nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreadDelegateLocalIsRemovedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void Run()
                                  {
                                      Action {|SST1497:callback|} = () => Console.WriteLine("never");
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public void Run()
                                       {
                                       }
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an awaited initializer survives the removal as an await statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedInitializerIsKeptAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public sealed class C
                              {
                                  public Task<int> LoadAsync() => Task.FromResult(1);

                                  public async Task RunAsync()
                                  {
                                      var {|SST1497:loaded|} = await LoadAsync();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public sealed class C
                                   {
                                       public Task<int> LoadAsync() => Task.FromResult(1);

                                       public async Task RunAsync()
                                       {
                                           await LoadAsync();
                                       }
                                   }
                                   """;
        await VerifyUnusedLocal.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local a nested scope only writes to is still unused.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalWrittenInsideALoopIsStillUnusedAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Run(int[] values)
                {
                    int {|SST1497:last|} = 0;
                    for (var i = 0; i < values.Length; i++)
                    {
                        last = values[i];
                    }
                }
            }
            """);

    /// <summary>Verifies a local declared straight into a switch section is analyzed against the whole switch.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Such a local is in scope across every section, so the scan climbs past the switch to the block around it.</remarks>
    [Test]
    public async Task LocalDeclaredInASwitchSectionIsCleanWhenReadAsync()
        => await VerifyUnusedLocal.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Run(int key)
                {
                    switch (key)
                    {
                        case 1:
                            int shared = 1;
                            Console.WriteLine(shared);
                            break;
                        default:
                            break;
                    }
                }
            }
            """);

    /// <summary>Verifies the discard is not written into source that predates it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The local really is unused, so the diagnostic stands. Below C# 7 there is no discard to keep the
    /// expression alive with, and the expression cannot stand as a statement, so no edit is offered.
    /// </remarks>
    [Test]
    public async Task DiscardIsNotOfferedBelowCSharp7Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Run(int[] values, int index)
                                  {
                                      var {|SST1497:element|} = values[index];
                                      return index;
                                  }
                              }
                              """;
        var test = new VerifyUnusedLocal.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a top-level statement's unused local is reported, and its whole global statement removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The blank line the declaration owned goes with it, as it does for any statement the fix deletes.</remarks>
    [Test]
    public async Task TopLevelStatementLocalIsReportedAsync()
    {
        var test = new VerifyUnusedLocal.Test
        {
            TestCode = """
                       using System;

                       int {|SST1497:unused|} = 1;
                       Console.WriteLine("done");
                       """,
            FixedCode = """
                        using System;
                        Console.WriteLine("done");
                        """,
        };

        test.TestState.OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication;
        test.FixedState.OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication;
        await test.RunAsync(CancellationToken.None);
    }
}
