// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using VerifyTimeProvider = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2010UseTimeProviderAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2010 (read the clock through a TimeProvider).</summary>
public class UseTimeProviderAnalyzerUnitTest
{
    /// <summary>Verifies every direct clock read inside a type is reported when TimeProvider is available.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryClockReadInsideATypeIsReportedAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                public DateTime A() => {|SST2010:DateTime.Now|};

                public DateTime B() => {|SST2010:DateTime.UtcNow|};

                public DateTimeOffset D() => {|SST2010:DateTimeOffset.Now|};

                public DateTimeOffset E() => {|SST2010:DateTimeOffset.UtcNow|};

                public int Hour() => {|SST2010:DateTime.UtcNow|}.Hour;
            }
            """);

    /// <summary>Verifies a type that already takes a TimeProvider reads the clock through it, and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadingThroughTheProviderIsCleanAsync()
        => await VerifyNet80Async(
            """
            using System;

            public class C
            {
                private readonly TimeProvider _time;

                public C(TimeProvider time) => _time = time;

                public DateTimeOffset Read() => _time.GetUtcNow();
            }
            """);

    /// <summary>Verifies a clock property on some other type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClockShapedMemberOfAnotherTypeIsCleanAsync()
        => await VerifyNet80Async(
            """
            using System;

            public static class Fake
            {
                public static DateTime Now => default;
            }

            public class C
            {
                public DateTime Read() => Fake.Now;
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework with no TimeProvider to suggest.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>TimeProvider arrived in .NET 8; on the .NET Standard 2.0 reference set there is nothing to move to.</remarks>
    [Test]
    public async Task NoDiagnosticWithoutTimeProviderAsync()
    {
        var test = new VerifyTimeProvider.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       using System;

                       public class C
                       {
                           public DateTime Read()
                           {
                               return DateTime.UtcNow;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the .NET 8 reference assemblies, where TimeProvider exists.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80Async(string source)
    {
        var test = new VerifyTimeProvider.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
