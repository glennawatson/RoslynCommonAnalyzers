// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1499 settings for one syntax tree.</summary>
/// <param name="IncludeInternal">Whether a field visible only inside the assembly is reported.</param>
internal readonly record struct MutableStaticFieldOptions(bool IncludeInternal)
{
    /// <summary>Whether an assembly-visible field is reported by default.</summary>
    public const bool DefaultIncludeInternal = true;

    /// <summary>The rule-specific internal-visibility key.</summary>
    private const string IncludeInternalRuleKey = "stylesharp.SST1499.include_internal";

    /// <summary>The project-wide internal-visibility key.</summary>
    private const string IncludeInternalGeneralKey = "stylesharp.include_internal";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the field's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// Assembly-visible fields are included by default: <c>internal</c> is a boundary between teams' code,
    /// not a boundary between threads, and a static field the whole assembly can reassign is exactly as
    /// shared as a public one. An unset or unparsable value keeps that default.
    /// </remarks>
    public static MutableStaticFieldOptions Read(AnalyzerConfigOptions options)
        => new(ReadBool(options, IncludeInternalRuleKey, IncludeInternalGeneralKey, DefaultIncludeInternal));

    /// <summary>Reads a boolean setting, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="fallback">The value used when neither key parses.</param>
    /// <returns>The configured value, or <paramref name="fallback"/>.</returns>
    private static bool ReadBool(AnalyzerConfigOptions options, string ruleKey, string generalKey, bool fallback)
    {
        if (options.TryGetValue(ruleKey, out var value) && bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return options.TryGetValue(generalKey, out value) && bool.TryParse(value, out parsed)
            ? parsed
            : fallback;
    }
}
