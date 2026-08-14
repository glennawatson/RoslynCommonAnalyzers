// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2337 settings for one syntax tree.</summary>
/// <param name="IncludePublicApi">Whether externally visible members are reported.</param>
internal readonly record struct UnreadParameterOptions(bool IncludePublicApi)
{
    /// <summary>The rule-specific public-API key.</summary>
    private const string IncludePublicApiRuleKey = "stylesharp.SST2337.unread_parameter_include_public_api";

    /// <summary>The project-wide public-API key.</summary>
    private const string IncludePublicApiGeneralKey = "stylesharp.unread_parameter_include_public_api";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the member's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// Externally visible members are excluded by default because removing a parameter there breaks every
    /// caller outside the assembly, and the diagnostic would be asking for a change the author may not be
    /// free to make. Set the key to <c>true</c> in an application, or before a major version, to see them.
    /// </remarks>
    public static UnreadParameterOptions Read(AnalyzerConfigOptions options)
        => new(ReadBool(options, IncludePublicApiRuleKey, IncludePublicApiGeneralKey, fallback: false));

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
