// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using VerifySplitTypes = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2017UseDateOnlyOrTimeOnlyAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2017 (use DateOnly or TimeOnly when only a date or a time of day is meant).</summary>
public class UseDateOnlyOrTimeOnlyAnalyzerUnitTest
{
    /// <summary>Verifies a Date read on a DateTime is reported wherever the receiver comes from.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DateReadIsReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                private readonly DateTime _created = new DateTime(2020, 1, 1);

                public DateTime FromParameter(DateTime when) => {|SST2017:when.Date|};

                public DateTime FromField() => {|SST2017:_created.Date|};

                public DateTime FromCall() => {|SST2017:Parse().Date|};

                public int Day() => {|SST2017:Parse().Date|}.Day;

                private static DateTime Parse() => default;
            }
            """);

    /// <summary>Verifies a TimeOfDay read on a DateTime is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TimeOfDayReadIsReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public TimeSpan OpeningTime(DateTime when) => {|SST2017:when.TimeOfDay|};
            }
            """);

    /// <summary>Verifies a clock read is left to SST2010 rather than reported twice on one line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClockReadReceiverIsNotReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public DateTime Today() => DateTime.Now.Date;

                public DateTime UtcToday() => DateTime.UtcNow.Date;

                public TimeSpan Elapsed() => DateTime.Now.TimeOfDay;
            }
            """);

    /// <summary>Verifies a DateTimeOffset receiver is not reported: naming its date means applying its offset first.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DateTimeOffsetReceiverIsNotReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public DateTime Day(DateTimeOffset when) => when.Date;

                public TimeSpan Time(DateTimeOffset when) => when.TimeOfDay;
            }
            """);

    /// <summary>Verifies a member of some other type that happens to be called Date is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMemberOfAnotherTypeIsCleanAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class Invoice
            {
                public DateTime Date { get; set; }

                public TimeSpan TimeOfDay { get; set; }
            }

            public class C
            {
                public DateTime Read(Invoice invoice) => invoice.Date;

                public TimeSpan ReadTime(Invoice invoice) => invoice.TimeOfDay;
            }
            """);

    /// <summary>Verifies the other components of a DateTime, and code already using the split types, are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComponentReadsAndSplitTypesAreCleanAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public int Day(DateTime when) => when.Day + when.Hour;

                public DateOnly Birthday(DateTime when) => DateOnly.FromDateTime(when);

                public TimeOnly Opening(DateTime when) => TimeOnly.FromDateTime(when);
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework with no DateOnly or TimeOnly to suggest.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Both types arrived in .NET 6; the .NET Standard 2.0 reference set has neither.</remarks>
    [Test]
    public async Task NoDiagnosticWithoutDateOnlyOrTimeOnlyAsync()
    {
        var test = new VerifySplitTypes.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       using System;

                       public class C
                       {
                           public DateTime Day(DateTime when)
                           {
                               return when.Date;
                           }

                           public TimeSpan Time(DateTime when)
                           {
                               return when.TimeOfDay;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the .NET 8 reference assemblies, where DateOnly and TimeOnly exist.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80Async(string source)
    {
        var test = new VerifySplitTypes.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
