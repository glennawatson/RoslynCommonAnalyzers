// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the documentation rule options from <c>.editorconfig</c>, using the same
/// CA-style provider-based approach as <see cref="NamingConventions"/> (general
/// key with a rule-specific override; no file is read directly).
/// </summary>
internal static class DocumentationOptions
{
    /// <summary>Rule-specific editorconfig key for the single-line summary length limit (SST1653).</summary>
    public const string SummaryMaxLengthSpecificKey = "stylesharp.SST1653.summary_single_line_max_length";

    /// <summary>General editorconfig key for the single-line summary length limit.</summary>
    public const string SummaryMaxLengthGeneralKey = "stylesharp.summary_single_line_max_length";

    /// <summary>The default maximum combined summary length, in characters, that should fit on one line.</summary>
    public const int DefaultSummaryMaxLength = 100;

    /// <summary>Reads the single-line summary length limit, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options for the relevant syntax tree.</param>
    /// <returns>The configured (or default) maximum length.</returns>
    public static int ReadSummaryMaxLength(AnalyzerConfigOptions options) =>
        TryReadPositiveInt(options, SummaryMaxLengthSpecificKey, out var value)
        || TryReadPositiveInt(options, SummaryMaxLengthGeneralKey, out value)
            ? value
            : DefaultSummaryMaxLength;

    /// <summary>Tries to read a positive integer from a single editorconfig key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="key">The editorconfig key.</param>
    /// <param name="value">The parsed value when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the key held a positive integer.</returns>
    private static bool TryReadPositiveInt(AnalyzerConfigOptions options, string key, out int value)
    {
        value = 0;
        return options.TryGetValue(key, out var text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }
}
