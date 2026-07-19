// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySelf = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2600LegacyTracingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2600 (application logging routed through legacy tracing).</summary>
public class LegacyTracingAnalyzerUnitTest
{
    /// <summary>Verifies a Trace.WriteLine call is reported when structured logging is available.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TraceWriteLineIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M() => {|SST2600:System.Diagnostics.Trace.WriteLine("x")|};
            }
            """));

    /// <summary>Verifies a Trace.Write call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TraceWriteIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M() => {|SST2600:System.Diagnostics.Trace.Write("x")|};
            }
            """));

    /// <summary>Verifies a Trace.WriteIf call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TraceWriteIfIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(bool flag) => {|SST2600:System.Diagnostics.Trace.WriteIf(flag, "x")|};
            }
            """));

    /// <summary>Verifies a Trace.WriteLineIf call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TraceWriteLineIfIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M(bool flag) => {|SST2600:System.Diagnostics.Trace.WriteLineIf(flag, "x")|};
            }
            """));

    /// <summary>Verifies the short Trace.WriteLine form binds and is reported through an imported namespace.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImportedTraceWriteLineIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            namespace App
            {
                using System.Diagnostics;

                public sealed class C
                {
                    public void M() => {|SST2600:Trace.WriteLine("x")|};
                }
            }
            """));

    /// <summary>Verifies a using-static Trace.WriteLine call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStaticTraceWriteLineIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            namespace App
            {
                using static System.Diagnostics.Trace;

                public sealed class C
                {
                    public void M() => {|SST2600:WriteLine("x")|};
                }
            }
            """));

    /// <summary>Verifies the rule stays silent when no structured-logging abstraction is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoStructuredLoggingAvailableIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M() => Trace.WriteLine("x");
            }
            """);

    /// <summary>Verifies Debug.WriteLine is not reported, since it is compiled out of release builds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DebugWriteLineIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M() => System.Diagnostics.Debug.WriteLine("x");
            }
            """));

    /// <summary>Verifies a Trace method outside the output set is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TraceInformationIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M() => System.Diagnostics.Trace.TraceInformation("x");
            }
            """));

    /// <summary>Verifies a same-named method on another type is not treated as legacy tracing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedTraceWriteLineIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public void M() => Trace.WriteLine("x");
            }

            public static class Trace
            {
                public static void WriteLine(string message)
                {
                }
            }
            """));
}
