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

    /// <summary>Editorconfig key controlling whether exposed (public/protected) elements require documentation.</summary>
    public const string DocumentExposedElementsKey = "stylesharp.document_exposed_elements";

    /// <summary>Editorconfig key controlling whether internal elements require documentation.</summary>
    public const string DocumentInternalElementsKey = "stylesharp.document_internal_elements";

    /// <summary>Editorconfig key controlling whether private elements require documentation.</summary>
    public const string DocumentPrivateElementsKey = "stylesharp.document_private_elements";

    /// <summary>Editorconfig key controlling how interfaces and their members are documented (<c>all</c>/<c>exposed</c>/<c>none</c>).</summary>
    public const string DocumentInterfacesKey = "stylesharp.document_interfaces";

    /// <summary>
    /// Reads the documentation-coverage scope (which accessibilities the SST1600/SST1601/SST1602/SST1654
    /// "must be documented" rules apply to) from <c>.editorconfig</c>. The defaults match StyleCop's
    /// <c>documentationRules</c>: exposed on, internal on, private off, interfaces <c>all</c>.
    /// </summary>
    /// <param name="options">The analyzer config options for the relevant syntax tree.</param>
    /// <returns>The configured (or default) coverage scope.</returns>
    public static DocumentationCoverage ReadCoverage(AnalyzerConfigOptions options) =>
        new(
            ReadBool(options, DocumentExposedElementsKey, defaultValue: true),
            ReadBool(options, DocumentInternalElementsKey, defaultValue: true),
            ReadBool(options, DocumentPrivateElementsKey, defaultValue: false),
            ReadInterfaceMode(options));

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

    /// <summary>Reads a boolean editorconfig key, falling back to the supplied default.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="key">The editorconfig key.</param>
    /// <param name="defaultValue">The value to use when the key is absent or unparseable.</param>
    /// <returns>The configured (or default) boolean.</returns>
    private static bool ReadBool(AnalyzerConfigOptions options, string key, bool defaultValue) =>
        options.TryGetValue(key, out var text) && bool.TryParse(text, out var value) ? value : defaultValue;

    /// <summary>Reads the interface documentation mode, accepting <c>all</c>/<c>exposed</c>/<c>none</c> (or <c>true</c>/<c>false</c>).</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <returns>The configured mode, or <see cref="DocumentationInterfaceMode.All"/> by default.</returns>
    private static DocumentationInterfaceMode ReadInterfaceMode(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(DocumentInterfacesKey, out var text))
        {
            return DocumentationInterfaceMode.All;
        }

        return text.ToLowerInvariant() switch
        {
            "all" or "true" => DocumentationInterfaceMode.All,
            "exposed" => DocumentationInterfaceMode.Exposed,
            "none" or "false" => DocumentationInterfaceMode.None,
            _ => DocumentationInterfaceMode.All,
        };
    }
}
