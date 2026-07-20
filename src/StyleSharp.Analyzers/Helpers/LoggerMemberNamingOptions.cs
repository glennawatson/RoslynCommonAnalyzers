// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads the accepted names for a private instance logger field (SST2601). The default set is <c>_logger</c>
/// and <c>_log</c>; a project overrides it with a comma-, semicolon-, or whitespace-separated list under
/// <c>stylesharp.SST2601.fieldname</c>. A non-private or static logger is always expected to be named
/// <c>Logger</c> and is not configurable. The value is read lazily — only after a member is confirmed to be a
/// private instance logger — so a project that never declares one pays nothing.
/// </summary>
internal static class LoggerMemberNamingOptions
{
    /// <summary>The name expected of a non-private or static logger member.</summary>
    public const string PublicName = "Logger";

    /// <summary>The default accepted names for a private instance logger field.</summary>
    public const string DefaultInstanceFieldNames = "_logger, _log";

    /// <summary>The name suggested for a private instance logger when the option is unset or unusable.</summary>
    private const string PrimaryInstanceName = "_logger";

    /// <summary>The editorconfig key overriding the accepted private-instance field names.</summary>
    private const string InstanceFieldNameKey = "stylesharp.SST2601.fieldname";

    /// <summary>Returns whether a name is one of the accepted private instance logger field names.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <param name="name">The member's name.</param>
    /// <returns><see langword="true"/> when the name is in the configured (or default) accepted list.</returns>
    public static bool IsAcceptedInstanceName(AnalyzerConfigOptions options, string name)
        => EditorConfigList.Contains(ReadConfigured(options) ?? DefaultInstanceFieldNames, name, StringComparison.Ordinal);

    /// <summary>Returns the private-instance logger name to suggest in a diagnostic.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The first configured accepted name, or <c>_logger</c> when none is usable.</returns>
    public static string PreferredInstanceName(AnalyzerConfigOptions options)
    {
        if (ReadConfigured(options) is not { } configured)
        {
            return PrimaryInstanceName;
        }

        var token = FirstToken(configured);
        return token.Length != 0 ? token : PrimaryInstanceName;
    }

    /// <summary>Returns the configured field-name list, or <see langword="null"/> when unset or empty.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The raw configured value, or <see langword="null"/>.</returns>
    private static string? ReadConfigured(AnalyzerConfigOptions options)
        => options.TryGetValue(InstanceFieldNameKey, out var value) && value.Length != 0 ? value : null;

    /// <summary>Returns the first token of a separated list, trimmed of surrounding whitespace.</summary>
    /// <param name="list">The raw list value.</param>
    /// <returns>The first token, or an empty string when the list holds none.</returns>
    private static string FirstToken(string list)
    {
        var start = 0;
        while (start < list.Length && IsSeparator(list[start]))
        {
            start++;
        }

        var end = start;
        while (end < list.Length && !IsSeparator(list[end]))
        {
            end++;
        }

        return end > start ? list.Substring(start, end - start) : string.Empty;
    }

    /// <summary>Returns whether a character separates list entries.</summary>
    /// <param name="value">The character to test.</param>
    /// <returns><see langword="true"/> for a comma, semicolon, space, or tab.</returns>
    private static bool IsSeparator(char value) => value is ',' or ';' or ' ' or '\t';
}
