// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the line-break placement and line-ending options for the configurable layout (SST15xx) rules.
/// Every rule takes a rule-specific key and falls back to a project-wide key of the same name, exactly
/// as the other configurable rules do. An unset or unrecognised value yields the supplied default, so a
/// typo neither disables a rule nor inverts it silently.
/// </summary>
internal static class LayoutStyleOptions
{
    /// <summary>The option value that places a token at the start of the continuation line.</summary>
    public const string BeforeValue = "before";

    /// <summary>The option value that places a token at the end of the upper line.</summary>
    public const string AfterValue = "after";

    /// <summary>The option value that selects line-feed line endings.</summary>
    public const string LineFeedValue = "lf";

    /// <summary>The option value that selects carriage-return/line-feed line endings.</summary>
    public const string CarriageReturnLineFeedValue = "crlf";

    /// <summary>The line-feed newline sequence.</summary>
    public const string LineFeed = "\n";

    /// <summary>The carriage-return/line-feed newline sequence.</summary>
    public const string CarriageReturnLineFeed = "\r\n";

    /// <summary>
    /// Reads whether a wrapped token should lead its continuation line (a break before it) rather than
    /// trail the upper line (a break after it).
    /// </summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="defaultBreakBefore">The value used when neither key resolves.</param>
    /// <returns><see langword="true"/> when the token should lead the continuation line.</returns>
    public static bool ReadBreakBefore(AnalyzerConfigOptions options, string ruleKey, string generalKey, bool defaultBreakBefore)
    {
        var value = ReadValue(options, ruleKey, generalKey);
        if (string.Equals(value, BeforeValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.Equals(value, AfterValue, StringComparison.OrdinalIgnoreCase) && defaultBreakBefore;
    }

    /// <summary>Reads the configured newline sequence for a file, defaulting to line feed.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The configured newline sequence.</returns>
    public static string ReadLineEnding(AnalyzerConfigOptions options, string ruleKey, string generalKey)
        => string.Equals(ReadValue(options, ruleKey, generalKey), CarriageReturnLineFeedValue, StringComparison.OrdinalIgnoreCase)
            ? CarriageReturnLineFeed
            : LineFeed;

    /// <summary>Returns the first non-empty configured value, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <returns>The configured value, or <see langword="null"/> when neither key is set.</returns>
    private static string? ReadValue(AnalyzerConfigOptions options, string ruleKey, string generalKey)
    {
        if (options.TryGetValue(ruleKey, out var value) && value.Length > 0)
        {
            return value;
        }

        return options.TryGetValue(generalKey, out value) && value.Length > 0 ? value : null;
    }
}
