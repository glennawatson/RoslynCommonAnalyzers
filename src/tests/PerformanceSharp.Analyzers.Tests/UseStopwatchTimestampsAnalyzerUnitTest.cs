// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1408UseStopwatchTimestampsAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1408UseStopwatchTimestampsAnalyzer"/> (PSH1408 stopwatch timestamps).</summary>
public class UseStopwatchTimestampsAnalyzerUnitTest
{
    /// <summary>Verifies a stopwatch used only for elapsed reads is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElapsedOnlyStopwatchIsFlaggedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public long M()
                {
                    var stopwatch = {|PSH1408:Stopwatch.StartNew()|};
                    DoWork();
                    return stopwatch.ElapsedMilliseconds;
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a stopped stopwatch that only reads elapsed time is still flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StoppedElapsedOnlyStopwatchIsFlaggedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Diagnostics;

            public class C
            {
                public TimeSpan M()
                {
                    var stopwatch = {|PSH1408:Stopwatch.StartNew()|};
                    DoWork();
                    stopwatch.Stop();
                    return stopwatch.Elapsed;
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a restarted stopwatch stays clean; timestamps cannot express Restart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RestartedStopwatchIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public long M()
                {
                    var stopwatch = Stopwatch.StartNew();
                    DoWork();
                    stopwatch.Restart();
                    DoWork();
                    return stopwatch.ElapsedMilliseconds;
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a stopwatch that escapes as an argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EscapingStopwatchIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M()
                {
                    var stopwatch = Stopwatch.StartNew();
                    Report(stopwatch);
                }

                private static void Report(Stopwatch stopwatch)
                {
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on frameworks without GetElapsedTime.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsGatedOnGetElapsedTimeAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            TestCode = """
                       using System.Diagnostics;

                       public class C
                       {
                           public long M()
                           {
                               var stopwatch = Stopwatch.StartNew();
                               return stopwatch.ElapsedMilliseconds;
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
