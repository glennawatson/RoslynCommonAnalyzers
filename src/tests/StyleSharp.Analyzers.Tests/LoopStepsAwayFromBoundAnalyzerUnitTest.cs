// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LoopConditionAnalyzer,
    StyleSharp.Analyzers.Sst2412LoopStepsAwayFromBoundCodeFixProvider>;
using VerifyLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.LoopConditionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2412 (a for loop steps its counter away from its bound).</summary>
public class LoopStepsAwayFromBoundAnalyzerUnitTest
{
    /// <summary>An ascending loop with a greater-than test.</summary>
    private const string AscendingSource = """
        using System;

        public sealed class C
        {
            public void M()
            {
                for (var i = 0; {|SST2412:i > 10|}; i++)
                {
                    Console.WriteLine(i);
                }
            }
        }
        """;

    /// <summary>The ascending loop after the fix.</summary>
    private const string AscendingFixed = """
        using System;

        public sealed class C
        {
            public void M()
            {
                for (var i = 0; i <= 10; i++)
                {
                    Console.WriteLine(i);
                }
            }
        }
        """;

    /// <summary>The reverse-index typo.</summary>
    private const string ReverseIndexSource = """
        using System;

        public sealed class C
        {
            public void M(int[] arr)
            {
                for (var i = arr.Length - 1; {|SST2412:i < 0|}; i--)
                {
                    Console.WriteLine(arr[i]);
                }
            }
        }
        """;

    /// <summary>The reverse-index typo after the fix.</summary>
    private const string ReverseIndexFixed = """
        using System;

        public sealed class C
        {
            public void M(int[] arr)
            {
                for (var i = arr.Length - 1; i >= 0; i--)
                {
                    Console.WriteLine(arr[i]);
                }
            }
        }
        """;

    /// <summary>Verifies an ascending step paired with a greater-than test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AscendingStepWithGreaterThanIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 0; {|SST2412:i > 10|}; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies the reverse-index typo (descending step, less-than test) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DescendingStepWithLessThanIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int[] arr)
                {
                    for (var i = arr.Length - 1; {|SST2412:i < 0|}; i--)
                    {
                        Console.WriteLine(arr[i]);
                    }
                }
            }
            """);

    /// <summary>Verifies the bound may sit on the left of the comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BoundOnTheLeftIsReportedAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 0; {|SST2412:10 < i|}; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies an ascending loop toward its bound is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AscendingTowardBoundIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a descending loop toward its bound is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DescendingTowardBoundIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 10; i > 0; i--)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a not-equal condition is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NotEqualConditionIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int n)
                {
                    for (var i = 0; i != n; i++)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a body that also re-steps the counter is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BodyRestepsCounterIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    for (var i = 0; i > 10; i++)
                    {
                        i -= 3;
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a non-constant step is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantStepIsCleanAsync() =>
        VerifyLoop.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int delta)
                {
                    for (var i = 0; i > 10; i += delta)
                    {
                        Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies the fix flips an ascending greater-than comparison to less-than-or-equal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixFlipsAscendingComparisonAsync() =>
        VerifyFix.VerifyCodeFixAsync(AscendingSource, AscendingFixed);

    /// <summary>Verifies the fix flips the reverse-index typo to greater-than-or-equal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixFlipsReverseIndexTypoAsync() =>
        VerifyFix.VerifyCodeFixAsync(ReverseIndexSource, ReverseIndexFixed);
}
