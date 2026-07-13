// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The resolved PSH1204 settings for one syntax tree.</summary>
/// <param name="Style">The replacement the fix should offer.</param>
/// <remarks>
/// The style the author asked for is not always the style the fix may emit: <see cref="EmptyStringStyle.Length"/>
/// and <see cref="EmptyStringStyle.IsNullOrEmpty"/> both disagree with <c>== ""</c> when the operand is null.
/// The analyzer therefore resolves the configured style against the operand's nullable flow state and writes the
/// style it actually settled on into the diagnostic, under <see cref="StyleKey"/>, for the fix to read back.
/// </remarks>
internal readonly record struct EmptyStringStyleOptions(EmptyStringStyle Style)
{
    /// <summary>The diagnostic property naming the style the fix should emit.</summary>
    public const string StyleKey = "EmptyStringStyle";

    /// <summary>The configuration value selecting the null-safe length pattern.</summary>
    public const string PatternValue = "pattern";

    /// <summary>The configuration value selecting the direct length test.</summary>
    public const string LengthValue = "length";

    /// <summary>The configuration value selecting <c>string.IsNullOrEmpty</c>.</summary>
    public const string IsNullOrEmptyValue = "is_null_or_empty";

    /// <summary>The rule-specific style key.</summary>
    private const string StyleRuleKey = "performancesharp.PSH1204.empty_string_style";

    /// <summary>The project-wide style key.</summary>
    private const string StyleGeneralKey = "performancesharp.empty_string_style";

    /// <summary>The diagnostic properties for each style, built once.</summary>
    private static readonly ImmutableDictionary<string, string?> PatternProperties = CreateProperties(PatternValue);

    /// <summary>The diagnostic properties for the direct length test, built once.</summary>
    private static readonly ImmutableDictionary<string, string?> LengthProperties = CreateProperties(LengthValue);

    /// <summary>The diagnostic properties for <c>string.IsNullOrEmpty</c>, built once.</summary>
    private static readonly ImmutableDictionary<string, string?> IsNullOrEmptyProperties = CreateProperties(IsNullOrEmptyValue);

    /// <summary>Reads the settings for one tree, falling back to the default.</summary>
    /// <param name="options">The analyzer config options for the comparison's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// The default is <see cref="EmptyStringStyle.Pattern"/> — the only one of the three that answers
    /// exactly what <c>== ""</c> answers for every input — so an unset or misspelled value can never
    /// change what a fixed file does when the string is null.
    /// </remarks>
    public static EmptyStringStyleOptions Read(AnalyzerConfigOptions options)
        => new(TryRead(options, StyleRuleKey, out var style) || TryRead(options, StyleGeneralKey, out style)
            ? style
            : EmptyStringStyle.Pattern);

    /// <summary>Gets the cached diagnostic properties that carry a style to the code fix.</summary>
    /// <param name="style">The style the analyzer settled on.</param>
    /// <returns>The properties to attach to the diagnostic.</returns>
    public static ImmutableDictionary<string, string?> GetProperties(EmptyStringStyle style) => style switch
    {
        EmptyStringStyle.Length => LengthProperties,
        EmptyStringStyle.IsNullOrEmpty => IsNullOrEmptyProperties,
        _ => PatternProperties,
    };

    /// <summary>Reads back the style the analyzer stored on a diagnostic.</summary>
    /// <param name="properties">The diagnostic's properties.</param>
    /// <returns>The stored style, or <see cref="EmptyStringStyle.Pattern"/> when none was stored.</returns>
    /// <remarks>
    /// The fallback matters: a diagnostic from an older build, or one whose properties were dropped, must
    /// fix to the exact-equivalent pattern rather than to a form that changes null behavior.
    /// </remarks>
    public static EmptyStringStyle ReadStyle(ImmutableDictionary<string, string?> properties)
        => properties.TryGetValue(StyleKey, out var value) && TryParse(value, out var style)
            ? style
            : EmptyStringStyle.Pattern;

    /// <summary>Builds the diagnostic properties naming one style.</summary>
    /// <param name="value">The style's configuration value.</param>
    /// <returns>The single-entry property map.</returns>
    private static ImmutableDictionary<string, string?> CreateProperties(string value)
        => ImmutableDictionary<string, string?>.Empty.Add(StyleKey, value);

    /// <summary>Tries to read and parse a style from a single editorconfig key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="key">The editorconfig key.</param>
    /// <param name="style">The parsed style when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the key was present and recognized.</returns>
    private static bool TryRead(AnalyzerConfigOptions options, string key, out EmptyStringStyle style)
    {
        if (options.TryGetValue(key, out var value))
        {
            return TryParse(value, out style);
        }

        style = EmptyStringStyle.Pattern;
        return false;
    }

    /// <summary>Parses a configured or stored style value.</summary>
    /// <param name="value">The value text.</param>
    /// <param name="style">The parsed style when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the value is recognized.</returns>
    private static bool TryParse(string? value, out EmptyStringStyle style)
    {
        if (string.Equals(value, LengthValue, StringComparison.OrdinalIgnoreCase))
        {
            style = EmptyStringStyle.Length;
            return true;
        }

        if (string.Equals(value, IsNullOrEmptyValue, StringComparison.OrdinalIgnoreCase))
        {
            style = EmptyStringStyle.IsNullOrEmpty;
            return true;
        }

        style = EmptyStringStyle.Pattern;
        return string.Equals(value, PatternValue, StringComparison.OrdinalIgnoreCase);
    }
}
