// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInvariantLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.LoopConditionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2406 (a loop whose stop condition can never change).</summary>
public class InvariantLoopConditionAnalyzerUnitTest
{
    /// <summary>Verifies a while loop whose counter is never advanced is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WhileWithoutAnAdvanceIsReportedAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int limit)
                {
                    var index = 0;
                    while ({|SST2406:index < limit|})
                    {
                        Console.WriteLine(index);
                    }
                }
            }
            """);

    /// <summary>Verifies a for loop that does not declare its counter and never advances it is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A for loop that declares its own counter is SST2411's shape; here the counter is declared outside the
    /// loop, so the invariant-condition rule is the one that applies.
    /// </remarks>
    [Test]
    public async Task ForWithoutAnIncrementorIsReportedAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int limit)
                {
                    var index = 0;
                    for (; {|SST2406:index < limit|};)
                    {
                        Console.WriteLine(index);
                    }
                }
            }
            """);

    /// <summary>Verifies a flag the body never sets is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsetFlagIsReportedAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    var done = false;
                    while ({|SST2406:!done|})
                    {
                        Console.WriteLine("working");
                    }
                }
            }
            """);

    /// <summary>Verifies a condition the body advances is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdvancedConditionIsCleanAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int limit)
                {
                    var index = 0;
                    while (index < limit)
                    {
                        Console.WriteLine(index);
                        index++;
                    }

                    for (var i = 0; i < limit; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a condition written through an <c>out</c> argument is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionWrittenByReferenceIsCleanAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(string text)
                {
                    var more = true;
                    while (more)
                    {
                        Console.WriteLine(text);
                        Advance(out more);
                    }
                }

                private static void Advance(out bool more) => more = false;
            }
            """);

    /// <summary>Verifies a loop with a real way out is clean, however fixed its condition is.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EarlyExitIsCleanAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Breaks(int limit)
                {
                    var index = 0;
                    while (index < limit)
                    {
                        Console.WriteLine(index);
                        break;
                    }
                }

                public int Returns(int limit)
                {
                    var index = 0;
                    while (index < limit)
                    {
                        return index;
                    }

                    return -1;
                }

                public void Throws(int limit)
                {
                    var index = 0;
                    while (index < limit)
                    {
                        throw new InvalidOperationException("stuck");
                    }
                }
            }
            """);

    /// <summary>Verifies a condition that calls something is clean: the call may answer differently each time.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionWithACallIsCleanAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(Queue<int> queue)
                {
                    while (queue.Count > 0)
                    {
                        Console.WriteLine(queue.Peek());
                        queue.Dequeue();
                    }
                }
            }
            """);

    /// <summary>Verifies a condition reading a field is clean: another thread, or a call, may write it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionReadingAFieldIsCleanAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private volatile bool _running = true;

                public void M()
                {
                    while (_running)
                    {
                        Console.WriteLine("working");
                    }
                }
            }
            """);

    /// <summary>Verifies a deliberate infinite loop is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeliberateInfiniteLoopIsCleanAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    while (true)
                    {
                        Console.WriteLine("forever");
                    }
                }
            }
            """);

    /// <summary>Verifies a body whose lambda could write the variable is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodyWithALambdaIsCleanAsync()
        => await VerifyInvariantLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(Action<Action> run)
                {
                    var done = false;
                    while (!done)
                    {
                        run(() => done = true);
                    }
                }
            }
            """);
}
