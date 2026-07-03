// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1304UsePeriodicTimerAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1304UsePeriodicTimerAnalyzer"/> (PSH1304 delay-paced polling loops).</summary>
public class UsePeriodicTimerAnalyzerUnitTest
{
    /// <summary>Verifies a delay pacing a while loop's tail is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelayPacedWhileLoopIsFlaggedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task BackoffLoopIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task ConditionalDelayIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task BoundedForLoopIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task DelayOnlyLoopBodyIsFlaggedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task DelayOutsideLoopIsCleanAsync()
        => await VerifyNet90Async(
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

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
