// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2338 settings for one syntax tree.</summary>
/// <param name="ReportValueTypePayloads">Whether a hand-rolled union holding value-typed payloads is reported.</param>
internal readonly record struct PreferUnionOptions(bool ReportValueTypePayloads)
{
    /// <summary>The rule-specific value-type payload key.</summary>
    private const string ReportValueTypePayloadsRuleKey = "stylesharp.SST2338.report_value_type_payloads";

    /// <summary>The project-wide value-type payload key.</summary>
    private const string ReportValueTypePayloadsGeneralKey = "stylesharp.report_value_type_payloads";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the declaration's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// A union stores its payload in a single object field, so a value-typed arm boxes on every
    /// construction. A hand-rolled tagged type holding value types is therefore left alone by default:
    /// taking the suggestion would trade a non-allocating type for one that allocates per instance,
    /// which matters most in exactly the hot types this shape is used for. Set the key to <c>true</c>
    /// where the clarity is worth the boxing. An unset or unparsable value keeps the default.
    /// </remarks>
    internal static PreferUnionOptions Read(AnalyzerConfigOptions options) =>
        new(AnalyzerOptionReader.ReadBool(options, ReportValueTypePayloadsRuleKey, ReportValueTypePayloadsGeneralKey, fallback: false));
}
