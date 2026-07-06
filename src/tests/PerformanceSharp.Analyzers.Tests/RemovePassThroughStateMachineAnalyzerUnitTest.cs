// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1311RemovePassThroughStateMachineAnalyzer,
    PerformanceSharp.Analyzers.Psh1311RemovePassThroughStateMachineCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1311RemovePassThroughStateMachineAnalyzer"/> (PSH1311 pass-through async state machine).</summary>
public class RemovePassThroughStateMachineAnalyzerUnitTest
{
    /// <summary>Verifies an expression-bodied pass-through await is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedPassThroughIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public {|PSH1311:async|} Task M() => await Inner();

                                  public Task Inner() => Task.CompletedTask;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M() => Inner();

                                       public Task Inner() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a lone <c>return await</c> of a matching generic task is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockReturnAwaitGenericTaskIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public {|PSH1311:async|} Task<int> M()
                                  {
                                      return await Inner();
                                  }

                                  public Task<int> Inner() => Task.FromResult(0);
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M()
                                       {
                                           return Inner();
                                       }

                                       public Task<int> Inner() => Task.FromResult(0);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a lone <c>await</c> statement in a Task-returning body is flagged and rewritten to a return.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StatementAwaitTaskIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public {|PSH1311:async|} Task M()
                                  {
                                      await Inner();
                                  }

                                  public Task Inner() => Task.CompletedTask;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Inner();
                                       }

                                       public Task Inner() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix strips a trailing ConfigureAwait from the forwarded task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfigureAwaitIsStrippedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public {|PSH1311:async|} Task M()
                                  {
                                      await Inner().ConfigureAwait(false);
                                  }

                                  public Task Inner() => Task.CompletedTask;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Inner();
                                       }

                                       public Task Inner() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a pass-through async local function is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionPassThroughIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      return Local();

                                      {|PSH1311:async|} Task Local() => await Inner();
                                  }

                                  public Task Inner() => Task.CompletedTask;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Local();

                                           Task Local() => Inner();
                                       }

                                       public Task Inner() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a body with a statement before the tail await stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoStatementBodyIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    System.Console.WriteLine("start");
                    await Inner();
                }

                public Task Inner() => Task.CompletedTask;
            }
            """);

    /// <summary>Verifies an await inside a using statement stays clean; the task must not outlive the resource.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitInsideUsingIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    using (var stream = new System.IO.MemoryStream())
                    {
                        await Inner();
                    }
                }

                public Task Inner() => Task.CompletedTask;
            }
            """);

    /// <summary>Verifies an async void method stays clean; there is no task to forward.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async void M() => await Inner();

                public Task Inner() => Task.CompletedTask;
            }
            """);

    /// <summary>Verifies awaiting a ValueTask in a Task-returning method stays clean; the types do not match.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueTaskAwaitInTaskMethodIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    await Inner();
                }

                public ValueTask Inner() => default;
            }
            """);

    /// <summary>Verifies a covariant generic forward stays clean; Task&lt;string&gt; is not Task&lt;object&gt;.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CovariantGenericTaskIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task<object> M()
                {
                    return await Inner();
                }

                public Task<string> Inner() => Task.FromResult("x");
            }
            """);

    /// <summary>Verifies an async lambda stays clean; only methods and local functions are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public Func<Task> M() => async () => await Inner();

                public Task Inner() => Task.CompletedTask;
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
