// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2600 descriptor.</summary>
internal static partial class LoggingRules
{
    /// <summary>SST2600 — application output written through legacy tracing instead of a structured logger.</summary>
    public static readonly DiagnosticDescriptor LegacyTracing = Create(
        "SST2600",
        "Application logging should use structured logging, not legacy tracing",
        "'Trace.{0}' routes application output through legacy tracing; write it through a structured logger ('ILogger') instead so it keeps a level, a category, and named state",
        LegacyTracingDescription);

    /// <summary>The LegacyTracing rule description.</summary>
    private const string LegacyTracingDescription =
        "A call to 'System.Diagnostics.Trace.Write', 'WriteLine', 'WriteIf', or 'WriteLineIf' emits application output "
        + "through the legacy tracing sink: a flat string with no severity, no category, and no structured fields, routed "
        + "only to whatever 'TraceListener' happens to be attached. Structured logging through 'ILogger' carries a level a "
        + "sink can filter on, a category a sink can route on, and named values a sink can index and query, so the same "
        + "message survives as data rather than a line of text. The rule reports only when structured logging is actually "
        + "available in the compilation, so the suggestion is one the project can act on, and only 'Trace.*' — not "
        + "'Debug.*', which is compiled out of release builds and never reaches production. The reported call binds to "
        + "'System.Diagnostics.Trace', so a same-named method on another type is left alone.";
}
