// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The resolved PSH1017 settings for one syntax tree.</summary>
/// <param name="ExcludedProperties">Property names the rule leaves alone, or an empty array.</param>
internal readonly record struct PropertyCopyOptions(string[] ExcludedProperties)
{
    /// <summary>The rule-specific excluded-properties key.</summary>
    private const string ExcludedRuleKey = "performancesharp.PSH1017.excluded_properties";

    /// <summary>The project-wide excluded-properties key.</summary>
    private const string ExcludedGeneralKey = "performancesharp.excluded_properties";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the property's tree.</param>
    /// <returns>The resolved settings.</returns>
    public static PropertyCopyOptions Read(AnalyzerConfigOptions options)
        => new(AnalyzerOptionReader.ReadCommaSeparatedList(options, ExcludedRuleKey, ExcludedGeneralKey));

    /// <summary>Returns whether a property name was configured away.</summary>
    /// <param name="name">The declared property name.</param>
    /// <returns><see langword="true"/> when the name appears in the configured exclusions.</returns>
    public bool IsExcluded(string name)
    {
        var excluded = ExcludedProperties;
        for (var i = 0; i < excluded.Length; i++)
        {
            if (string.Equals(excluded[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
