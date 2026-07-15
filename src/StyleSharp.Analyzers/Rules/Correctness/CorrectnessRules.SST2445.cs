// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2445 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2445 — a custom date/time format uses an unquoted culture separator with a culture-sensitive provider.</summary>
    public static readonly DiagnosticDescriptor CultureSensitiveDateFormat = Create(
        "SST2445",
        "A custom date/time format should not depend on the culture's separators",
        CultureSensitiveDateFormatMessage,
        CultureSensitiveDateFormatDescription);

    /// <summary>The CultureSensitiveDateFormat message format.</summary>
    private const string CultureSensitiveDateFormatMessage =
        "This custom date/time format uses an unquoted '/' or ':', which renders as the current culture's separator; quote the "
        + "separator or pass the invariant culture for a fixed format, or use a standard specifier for a localized one";

    /// <summary>The CultureSensitiveDateFormat rule description.</summary>
    private const string CultureSensitiveDateFormatDescription =
        "In a custom date/time format string, an unquoted '/' means \"the current culture's date separator\" and an unquoted ':' means "
        + "\"the current culture's time separator\" — not a literal slash or colon. So a format written as day/month/year renders with dots "
        + "under one culture and dashes under another, and a value meant for a wire format or a file name silently changes shape when the "
        + "process runs somewhere else. The defect is reported only when a culture-sensitive provider is in play: an explicit current-culture "
        + "provider on a method call, or the implicit current culture of a plain interpolated string. A call that passes the invariant culture "
        + "is correct and is never reported, and a call that passes no provider at all is out of scope. To keep a fixed shape, quote the "
        + "separator or pass the invariant culture; to show a localized date, use a standard specifier, which is already localized.";
}
