// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1303NoThreadSleepInAsyncAnalyzer,
    PerformanceSharp.Analyzers.Psh1303NoThreadSleepInAsyncCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1303NoThreadSleepInAsyncAnalyzer"/> (PSH1303 Thread.Sleep in async).</summary>
public class NoThreadSleepInAsyncAnalyzerUnitTest
{
    /// <summary>Verifies a sleep in an async method is flagged and rewritten to an awaited delay.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SleepInAsyncMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1303:Thread.Sleep(100)|};
                                      await Task.Yield();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await Task.Delay(100);
                                           await Task.Yield();
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a sleep in a synchronous method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SleepInSynchronousMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Threading;

            public class C
            {
                public void M()
                {
                    Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Verifies a sleep in an async lambda is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SleepInAsyncLambdaIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public class C
            {
                public Func<Task> M()
                    => async () =>
                    {
                        {|PSH1303:Thread.Sleep(100)|};
                        await Task.Yield();
                    };
            }
            """);

    /// <summary>Verifies a sleep in a synchronous local function inside an async method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SleepInSynchronousLocalFunctionIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    Pause();
                    await Task.Yield();

                    static void Pause() => Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Verifies the TimeSpan overload carries over to the delay unchanged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TimeSpanOverloadIsFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M()
                                  {
                                      {|PSH1303:Thread.Sleep(TimeSpan.FromSeconds(1))|};
                                      await Task.Yield();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M()
                                       {
                                           await Task.Delay(TimeSpan.FromSeconds(1));
                                           await Task.Yield();
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a user-defined Thread type is clean because the call never binds to the runtime's.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedThreadTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public static class Thread
            {
                public static void Sleep(int milliseconds)
                {
                }
            }

            public class C
            {
                public async Task M()
                {
                    Thread.Sleep(100);
                    await Task.Yield();
                }
            }
            """);
}
