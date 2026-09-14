// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1304UsePeriodicTimerAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1304UsePeriodicTimerAnalyzer"/> (PSH1304 delay-paced polling loops).</summary>
public class UsePeriodicTimerAnalyzerUnitTest
{
    /// <summary>Checks loop shape, bounded conditions, and writes affecting the delay.</summary>
    /// <param name="body">The asynchronous method body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("do { {|PSH1304:await Task.Delay(1)|}; } while (flag);")]
    [Arguments("do {|PSH1304:await Task.Delay(1)|}; while (flag);")]
    [Arguments("while (flag) {|PSH1304:await System.Threading.Tasks.Task.Delay(1)|};")]
    [Arguments("do { await Task.Delay(1); } while (delay <= 10);")]
    [Arguments("while (delay > 0) await Task.Delay(1);")]
    [Arguments("while (flag && delay >= 0) await Task.Delay(1);")]
    [Arguments("while ((flag == true) && (delay < 10)) await Task.Delay(1);")]
    [Arguments("while (flag == true) { delay++; {|PSH1304:await Task.Delay(1)|}; }")]
    [Arguments("while (flag) { other = delay; {|PSH1304:await Task.Delay(delay)|}; }")]
    [Arguments("while (flag) { delay++; await Task.Delay(delay); }")]
    [Arguments("while (flag) { --delay; await Task.Delay(delay); }")]
    [Arguments("while (flag) { Change(ref delay); await Task.Delay(delay); }")]
    [Arguments("while (flag) { {|PSH1304:await Task.Delay(Math.Abs(delay))|}; other++; }")]
    [Arguments("while (flag) { if (flag) await Task.Delay(1); }")]
    [Arguments("while (flag) { await Task.Yield(); }")]
    [Arguments("while (flag) { await pending; }")]
    [Arguments("while (flag) { await Delay(1); }")]
    [Arguments("while (flag) { await Task<int>.Delay(1); }")]
    [Arguments("while (flag) { Func<Task> work = async () => await Task.Delay(1); await work(); }")]
    public Task PacingRequiresUnboundedUnconditionalStableDelayAsync(string body) =>
        VerifyNet90Async($$"""
            using System;
            using System.Threading.Tasks;
            using static System.Threading.Tasks.Task;
            class C
            {
                async Task M(bool flag, int delay, int other, Task pending) { {{body}} }
                static void Change(ref int value) {}
            }
            """);

    /// <summary>Checks a Task-shaped receiver from another type is rejected semantically.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LookalikeTaskDelayIsCleanAsync() => VerifyNet90Async(
        """
        class Task
        {
            public static System.Threading.Tasks.Task Delay(int value) => System.Threading.Tasks.Task.CompletedTask;
            async System.Threading.Tasks.Task M() { while (true) { await Task.Delay(1); } }
        }
        """);

    /// <summary>Verifies a delay pacing a while loop's tail is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelayPacedWhileLoopIsFlaggedAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    while (true)
                    {
                        DoWork();
                        {|PSH1304:await Task.Delay(1000)|};
                    }
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a retry loop that adjusts its delay stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BackoffLoopIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    var delay = 100;
                    while (true)
                    {
                        DoWork();
                        await Task.Delay(delay);
                        delay *= 2;
                    }
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a conditional delay inside the loop stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConditionalDelayIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(bool slow)
                {
                    while (true)
                    {
                        DoWork();
                        if (slow)
                        {
                            await Task.Delay(1000);
                        }
                    }
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a bounded for loop stays clean because it is usually retry logic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BoundedForLoopIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        DoWork();
                        await Task.Delay(1000);
                    }
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a delay-only spin loop body is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelayOnlyLoopBodyIsFlaggedAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                private bool _done;

                public async Task M()
                {
                    while (!_done)
                    {
                        {|PSH1304:await Task.Delay(50)|};
                    }
                }
            }
            """);

    /// <summary>Verifies a delay outside any loop stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelayOutsideLoopIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    await Task.Delay(1000);
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on frameworks without PeriodicTimer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsGatedOnPeriodicTimerAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            TestCode = """
                       using System.Threading.Tasks;

                       public class C
                       {
                           public async Task M()
                           {
                               while (true)
                               {
                                   await Task.Delay(1000);
                               }
                           }
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a deadline-bounded poll is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeadlineBoundedPollIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<bool> WaitForAsync(Func<bool> ready, TimeSpan timeout)
                {
                    var deadline = DateTime.UtcNow + timeout;
                    while (DateTime.UtcNow < deadline)
                    {
                        if (ready())
                        {
                            return true;
                        }

                        await Task.Delay(50);
                    }

                    return false;
                }
            }
            """);

    /// <summary>Verifies an elapsed-time bounded poll is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ElapsedBoundedPollIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;
            using System.Diagnostics;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<bool> WaitForAsync(Func<bool> ready, TimeSpan timeout)
                {
                    var watch = Stopwatch.StartNew();
                    while (watch.Elapsed < timeout && !ready())
                    {
                        await Task.Delay(50);
                    }

                    return ready();
                }
            }
            """);

    /// <summary>Verifies an attempt-bounded retry loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AttemptBoundedLoopIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<bool> TryAsync(Func<bool> ready, int maxAttempts)
                {
                    var attempt = 0;
                    while (attempt < maxAttempts)
                    {
                        if (ready())
                        {
                            return true;
                        }

                        attempt++;
                        await Task.Delay(100);
                    }

                    return false;
                }
            }
            """);

    /// <summary>Verifies an unbounded cancellation-driven loop is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CancellationDrivenLoopIsStillReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Threading;
            using System.Threading.Tasks;

            public class C
            {
                public async Task RunAsync(CancellationToken token)
                {
                    while (!token.IsCancellationRequested)
                    {
                        Work();
                        {|PSH1304:await Task.Delay(1000)|};
                    }
                }

                private static void Work()
                {
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        await test.RunAsync(CancellationToken.None);
    }
}
