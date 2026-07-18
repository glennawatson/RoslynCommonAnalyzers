// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUncheckedSum = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2457UncheckedSequenceSumAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2457 (a sequence Sum wrapped in unchecked still throws on overflow).</summary>
public class UncheckedSequenceSumAnalyzerUnitTest
{
    /// <summary>Verifies a Sum call wrapped in an unchecked expression is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UncheckedExpressionSumIsReportedAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public int Total(int[] values) => unchecked({|SST2457:values.Sum()|});
            }
            """);

    /// <summary>Verifies a Sum call inside an unchecked block is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UncheckedBlockSumIsReportedAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public long Total(long[] values)
                {
                    unchecked
                    {
                        return {|SST2457:values.Sum()|} + 1;
                    }
                }
            }
            """);

    /// <summary>Verifies the nullable-int Sum overload is reported: it uses the same internal checked arithmetic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableIntSumIsReportedAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public int? Total(int?[] values) => unchecked({|SST2457:values.Sum()|});
            }
            """);

    /// <summary>Verifies the selector overload is reported: the projected values are still summed with checked arithmetic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelectorSumIsReportedAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public long Total(string[] values) => unchecked({|SST2457:values.Sum(v => (long)v.Length)|});
            }
            """);

    /// <summary>Verifies a static (non-extension) call to the Sum operator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticInvocationSumIsReportedAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public int Total(int[] values) => unchecked({|SST2457:Enumerable.Sum(values)|});
            }
            """);

    /// <summary>Verifies a Sum call with no unchecked wrapper is clean: the wrapper is the problem, not the call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareSumIsCleanAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public int Total(int[] values) => values.Sum();
            }
            """);

    /// <summary>Verifies unchecked arithmetic without a Sum call is clean: there the wrapper really does wrap around.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UncheckedArithmeticIsCleanAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Add(int a, int b) => unchecked(a + b);
            }
            """);

    /// <summary>Verifies the double Sum overload is clean: floating-point addition never throws on overflow.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DoubleSumInUncheckedIsCleanAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public double Total(double[] values) => unchecked(values.Sum());
            }
            """);

    /// <summary>Verifies the decimal Sum overload is clean: decimal overflow is not this rule's territory.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecimalSumInUncheckedIsCleanAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public decimal Total(decimal[] values) => unchecked(values.Sum());
            }
            """);

    /// <summary>Verifies a user-defined Sum method is clean: only the sequence operator is known to check internally.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedSumInUncheckedIsCleanAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            public sealed class Accumulator
            {
                public int Sum() => 0;

                public int Total() => unchecked(Sum() + 1);
            }
            """);

    /// <summary>Verifies a Sum whose nearest wrapper is checked is clean: that wrapper claims nothing about wraparound.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CheckedInsideUncheckedIsCleanAsync()
        => await VerifyUncheckedSum.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public sealed class C
            {
                public int Total(int[] values)
                {
                    unchecked
                    {
                        return checked(values.Sum());
                    }
                }
            }
            """);
}
