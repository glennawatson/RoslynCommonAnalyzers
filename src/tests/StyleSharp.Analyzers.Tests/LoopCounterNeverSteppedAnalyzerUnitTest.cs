// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.LoopConditionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2411 (a for loop declares and tests a counter it never advances).</summary>
public class LoopCounterNeverSteppedAnalyzerUnitTest
{
    /// <summary>Verifies a for loop with an empty incrementer that never touches the counter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyIncrementerIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int limit)
                {
                    for (var i = 0; {|SST2411:i < limit|};)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a for loop that steps the wrong variable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StepsTheWrongVariableIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    var total = 0;
                    for (var i = 0; {|SST2411:i < 10|}; total++)
                    {
                        total += i;
                    }
                }
            }
            """);

    /// <summary>Verifies a declared, never-stepped counter is reported even when the body can break out.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportedEvenWithABreakAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int limit)
                {
                    for (var i = 0; {|SST2411:i < limit|};)
                    {
                        if (i == 5)
                        {
                            break;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a counter advanced in the incrementer is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SteppedCounterIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int limit)
                {
                    for (var i = 0; i < limit; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a bound local declared alongside a stepped counter is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclaredBoundBesideSteppedCounterIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items)
                {
                    for (int i = 0, n = items.Count; i < n; i++)
                    {
                        Console.WriteLine(items[i]);
                    }
                }
            }
            """);

    /// <summary>Verifies a flag tested with a break, where the counter is not tested, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FlagWithBreakIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(bool done, int stop)
                {
                    for (var i = 0; !done; i++)
                    {
                        if (i == stop)
                        {
                            break;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a condition that reads a field is clean: something else may change it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionReadingAFieldIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private int _limit;

                public void M()
                {
                    for (var i = 0; i < _limit;)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a body whose lambda could advance the counter is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodyWithALambdaIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int limit, Action<Action> run)
                {
                    for (var i = 0; i < limit;)
                    {
                        run(() => i++);
                    }
                }
            }
            """);
}
