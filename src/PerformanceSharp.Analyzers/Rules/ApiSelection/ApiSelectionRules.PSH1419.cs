// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1419 descriptor.</summary>
internal static partial class ApiSelectionRules
{
    /// <summary>PSH1419 — prefer the built-in cross-platform time-zone API over a converter package.</summary>
    public static readonly DiagnosticDescriptor PreferBuiltInTimeZone = Create(
        "PSH1419",
        "Use the built-in cross-platform time-zone API",
        "Use '{0}' instead of the TimeZoneConverter package",
        PreferBuiltInTimeZoneDescription);

    /// <summary>The PSH1419 rule description.</summary>
    private const string PreferBuiltInTimeZoneDescription =
        "Since .NET 6 the runtime resolves both IANA and Windows time-zone ids on every platform: "
        + "'TimeZoneInfo.FindSystemTimeZoneById' accepts either id style, and "
        + "'TimeZoneInfo.TryConvertIanaIdToWindowsId' / 'TimeZoneInfo.TryConvertWindowsIdToIanaId' convert between "
        + "the two. The TimeZoneConverter package predates that support and is now an avoidable dependency — a "
        + "package to restore, load, and keep current for work the framework already does. The built-in members are "
        + "resolved in the analyzed compilation before anything is reported, so a project targeting a framework that "
        + "lacks a replacement is never handed a suggestion it cannot take; the id-conversion calls are reported only "
        + "where the conversion helpers exist.";
}
