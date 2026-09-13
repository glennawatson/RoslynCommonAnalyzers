// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using VerifyLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2465LoopConditionVariableReassignedAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2465 (a for loop's body reassigns a variable its condition depends on).</summary>
public class LoopConditionVariableReassignedAnalyzerUnitTest
{
    /// <summary>Verifies a body assignment to the loop's bound local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BoundReassignedInBodyIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CounterReassignedInBodyIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CounterCompoundAssignedInBodyIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BoundDecrementedInBodyIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleStatementBodyCounterReassignedIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedBareBlockCounterReassignedIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WellFormedLoopIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CounterReassignedInsideIfIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BoundReassignedInsideNestedLoopIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WriteToUnrelatedLocalIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WriteToConditionFieldIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BodyDrivenLoopWithNoIncrementerIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoConditionIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConditionWithCallBoundIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompoundConditionIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultipleIncrementersIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CounterReassignedInsideLambdaIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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

    /// <summary>Verifies counted comparisons recognize prefix, postfix, and assignment incrementers.</summary>
    /// <param name="condition">The simple comparison in the loop header.</param>
    /// <param name="incrementor">The counter step in the loop header.</param>
    /// <param name="write">The unconditional write that invalidates the comparison.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("i <= n", "++i", "++n")]
    [Arguments("i > n", "--i", "--n")]
    [Arguments("i >= n", "i--", "n++")]
    [Arguments("i == n", "i += 2", "n = 0")]
    [Arguments("i != n", "i = i + 1", "n = 0")]
    [Arguments("i < -n", "i++", "n = 0")]
    [Arguments("i < (n + a + b)", "i++", "b = 0")]
    public Task CountedLoopFormsReportUnconditionalWritesAsync(string condition, string incrementor, string write) =>
        VerifyLoop.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public void M(int n, int a, int b)
                {
                    for (int i = 0; {{condition}}; {{incrementor}})
                        {|SST2465:{{write}}|};
                }
            }
            """);

    /// <summary>Verifies ambiguous counters and conditions are rejected before inspecting writes.</summary>
    /// <param name="condition">The unsupported loop condition.</param>
    /// <param name="incrementor">The incrementer whose counter may be unrecognized or unrelated.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("true", "i++")]
    [Arguments("1 < 2", "i++")]
    [Arguments("i < n + a + b + c", "i++")]
    [Arguments("n < a", "i++")]
    [Arguments("i < ++n", "i++")]
    [Arguments("i < n++", "i++")]
    [Arguments("i < Limit()", "i++")]
    [Arguments("i < n", "Limit()")]
    [Arguments("i < n", "this.Counter++")]
    [Arguments("i < n", "++this.Counter")]
    [Arguments("i < n", "this.Counter += 1")]
    public Task UnsupportedCountedLoopFormsAreCleanAsync(string condition, string incrementor) =>
        VerifyLoop.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public int Counter;
                public int Limit() => 10;
                public void M(int n, int a, int b, int c)
                {
                    for (int i = 0; {{condition}}; {{incrementor}})
                        n = 0;
                }
            }
            """);

    /// <summary>Verifies guarded writes, member writes, and non-writing statements remain silent.</summary>
    /// <param name="body">The body that does not unconditionally assign a condition local or parameter.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("{ }")]
    [Arguments(";")]
    [Arguments("{ this.Counter = 0; ++this.Counter; this.Counter++; }")]
    [Arguments("{ switch (n) { case 0: n = 1; break; } }")]
    [Arguments("{ try { n = 0; } finally { } }")]
    [Arguments("{ int ignored = n; }")]
    public Task NonWritingAndGuardedBodiesAreCleanAsync(string body) =>
        VerifyLoop.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public int Counter;
                public void M(int n)
                {
                    for (int i = 0; i < n; i++)
                        {{body}}
                }
            }
            """);

    /// <summary>Verifies incomplete operators and unresolved or constant write targets remain silent.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InvalidWritesAndIncrementersAreCleanAsync()
    {
        var test = new VerifyLoop.Test
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = """
                class C
                {
                    void M(int n)
                    {
                        const int limit = 10;
                        for (int i = 0; i < limit; i++) { limit = 0; }
                        for (int i = 0; i < missing; i++) { missing = 0; }
                        for (int i = 0; i < n; i++) { -n; n!; }
                        for (int i = 0; i < n; i!) { n = 0; }
                        for (int i = 0; i < n; -i) { n = 0; }
                    }
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
