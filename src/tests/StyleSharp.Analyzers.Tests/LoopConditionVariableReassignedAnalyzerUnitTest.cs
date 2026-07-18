// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2465LoopConditionVariableReassignedAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2465 (a for loop's body reassigns a variable its condition depends on).</summary>
public class LoopConditionVariableReassignedAnalyzerUnitTest
{
    /// <summary>Verifies a body assignment to the loop's bound local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoundReassignedInBodyIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> list)
                {
                    var n = list.Count;
                    for (var i = 0; i < n; i++)
                    {
                        Console.WriteLine(i);
                        {|SST2465:n = 0|};
                    }
                }
            }
            """);

    /// <summary>Verifies a body assignment to the counter itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterReassignedInBodyIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Console.WriteLine(i);
                        {|SST2465:i = 5|};
                    }
                }
            }
            """);

    /// <summary>Verifies a body compound assignment to the counter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterCompoundAssignedInBodyIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                        {|SST2465:i += 2|};
                    }
                }
            }
            """);

    /// <summary>Verifies a body decrement of the bound parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoundDecrementedInBodyIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                        {|SST2465:n--|};
                    }
                }
            }
            """);

    /// <summary>Verifies an unconditional counter reassignment in a single-statement body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleStatementBodyCounterReassignedIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M()
                {
                    for (var i = 0; i < 10; i++)
                        {|SST2465:i = 9|};
                }
            }
            """);

    /// <summary>Verifies a counter reassignment inside a bare nested block is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedBareBlockCounterReassignedIsReportedAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                        {
                            {|SST2465:i = 0|};
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a well-formed counted loop that only reads its variables is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WellFormedLoopIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a counter reassignment guarded by an if is left alone (possible early advance).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterReassignedInsideIfIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int n, bool done)
                {
                    for (var i = 0; i < n; i++)
                    {
                        if (done)
                        {
                            i = n;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a bound reassignment guarded by a nested loop is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoundReassignedInsideNestedLoopIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                        while (n > 0)
                        {
                            n = 0;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a write to a non-condition local is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteToUnrelatedLocalIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int n)
                {
                    var total = 0;
                    for (var i = 0; i < n; i++)
                    {
                        total += i;
                    }
                }
            }
            """);

    /// <summary>Verifies a write to a field the condition reads is clean: something else may own it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteToConditionFieldIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    for (var i = 0; i < _n; i++)
                    {
                        _n = 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a loop that steps its counter in the body with no incrementer is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodyDrivenLoopWithNoIncrementerIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 0; i < n;)
                    {
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a loop with no condition is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoConditionIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M()
                {
                    for (var i = 0; ; i++)
                    {
                        i = 0;
                        if (i > 100)
                        {
                            break;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a condition with a method-call bound is clean: its shape is not the counted form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionWithCallBoundIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> list)
                {
                    var n = 0;
                    for (var i = 0; i < list.Count; i++)
                    {
                        n = 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a compound condition is clean: the controlling variable is not unambiguous.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundConditionIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int n, bool go)
                {
                    for (var i = 0; i < n && go; i++)
                    {
                        n = 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a two-counter loop with multiple incrementers is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleIncrementersIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(int n)
                {
                    for (int i = 0, j = n; i < j; i++, j--)
                    {
                        i = 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a counter reassignment inside a body lambda is clean: it does not run in loop order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterReassignedInsideLambdaIsCleanAsync()
        => await VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int n, Action<Action> run)
                {
                    for (var i = 0; i < n; i++)
                    {
                        run(() => i = 0);
                    }
                }
            }
            """);
}
