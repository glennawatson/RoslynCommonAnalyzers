// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1021ForcedGarbageCollectionAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1021ForcedGarbageCollectionAnalyzer"/> (PSH1021 forced garbage collection).</summary>
public class ForcedGarbageCollectionAnalyzerUnitTest
{
    /// <summary>Verifies a parameterless <c>GC.Collect()</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GcCollectIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class C
            {
                public static void M() => {|PSH1021:GC.Collect()|};
            }
            """);

    /// <summary>Verifies a <c>GC.Collect(generation)</c> overload is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GcCollectWithGenerationArgumentIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class C
            {
                public static void M() => {|PSH1021:GC.Collect(2)|};
            }
            """);

    /// <summary>Verifies a <c>GC.WaitForPendingFinalizers()</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GcWaitForPendingFinalizersIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class C
            {
                public static void M() => {|PSH1021:GC.WaitForPendingFinalizers()|};
            }
            """);

    /// <summary>Verifies a fully qualified <c>System.GC.Collect()</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedGcCollectIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static void M() => {|PSH1021:System.GC.Collect()|};
            }
            """);

    /// <summary>Verifies a same-named user <c>GC.Collect()</c> is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserGcCollectIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            namespace My
            {
                public static class GC
                {
                    public static void Collect()
                    {
                    }
                }

                public static class C
                {
                    public static void M() => GC.Collect();
                }
            }
            """);

    /// <summary>Verifies an unrelated call is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedCallIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public static class C
            {
                public static void M()
                {
                    var items = new List<int>();
                    items.Add(1);
                }
            }
            """);

    /// <summary>Verifies forced-GC calls that settle the heap for an allocation sample are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForcedGcInAllocationMeasurementIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class C
            {
                public static long Measure(Action op)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    var before = GC.GetAllocatedBytesForCurrentThread();
                    op();
                    return GC.GetAllocatedBytesForCurrentThread() - before;
                }
            }
            """);

    /// <summary>Verifies a forced collection beside a <c>GC.GetTotalMemory</c> read is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForcedGcBesideGetTotalMemoryIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class C
            {
                public static long Measure()
                {
                    GC.Collect();
                    return GC.GetTotalMemory(true);
                }
            }
            """);

    /// <summary>Verifies a same-named sampling method that is not <c>System.GC</c> does not exempt the forced collection.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForcedGcWithLookalikeSampleIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class Meter
            {
                public static long GetAllocatedBytesForCurrentThread() => 0;
            }

            public static class C
            {
                public static long Measure()
                {
                    {|PSH1021:GC.Collect()|};
                    return Meter.GetAllocatedBytesForCurrentThread();
                }
            }
            """);
}
