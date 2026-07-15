// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDiscarded = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2418DiscardedImmutableResultAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2418 (the discarded result of an immutable value's method).</summary>
public class DiscardedImmutableResultAnalyzerUnitTest
{
    /// <summary>Verifies a discarded DateTime method result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardedDateTimeResultIsReportedAsync()
        => await VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(DateTime date)
                {
                    {|SST2418:date.AddDays(1)|};
                }
            }
            """);

    /// <summary>Verifies a discarded static numeric-helper result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardedMathResultIsReportedAsync()
        => await VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    {|SST2418:Math.Abs(-5)|};
                }
            }
            """);

    /// <summary>Verifies a discarded readonly-record-struct result is reported, derived from the type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardedReadonlyRecordStructResultIsReportedAsync()
        => await VerifyDiscarded.VerifyAnalyzerAsync(
            """
            public readonly record struct Money(int Amount)
            {
                public Money Add(Money other) => new Money(Amount + other.Amount);
            }

            public sealed class C
            {
                public void M(Money money, Money other)
                {
                    {|SST2418:money.Add(other)|};
                }
            }
            """);

    /// <summary>Verifies a discarded string result is left to the unused-string diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardedStringResultIsCleanAsync()
        => await VerifyDiscarded.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(string text)
                {
                    text.Trim();
                }
            }
            """);

    /// <summary>Verifies a void mutating method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidMethodIsCleanAsync()
        => await VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items)
                {
                    items.Add(1);
                }
            }
            """);

    /// <summary>Verifies a used result is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsedResultIsCleanAsync()
        => await VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public DateTime M(DateTime date) => date.AddDays(1);
            }
            """);
}
