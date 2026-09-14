// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1488 settings for one syntax tree.</summary>
/// <param name="RequireParameterless">Whether an exception type must declare a parameterless constructor.</param>
/// <param name="IncludeNonPublicTypes">Whether an exception type that is not visible outside the assembly is checked.</param>
internal readonly record struct ExceptionConstructorOptions(
    bool RequireParameterless,
    bool IncludeNonPublicTypes)
{
    /// <summary>The rule-specific parameterless key.</summary>
    private const string RequireParameterlessRuleKey = "stylesharp.SST1488.require_parameterless";

    /// <summary>The project-wide parameterless key.</summary>
    private const string RequireParameterlessGeneralKey = "stylesharp.require_parameterless";

    /// <summary>The rule-specific non-public-type key.</summary>
    private const string IncludeNonPublicTypesRuleKey = "stylesharp.SST1488.include_non_public_types";

    /// <summary>The project-wide non-public-type key.</summary>
    private const string IncludeNonPublicTypesGeneralKey = "stylesharp.include_non_public_types";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the declaration's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// Both settings default to the stricter reading: every exception type is checked, and all three
    /// constructors are asked for. An unset or unparsable value yields that default, so a typo cannot
    /// quietly narrow the rule to nothing.
    /// </remarks>
    internal static ExceptionConstructorOptions Read(AnalyzerConfigOptions options) => new(
        AnalyzerOptionReader.ReadBool(options, RequireParameterlessRuleKey, RequireParameterlessGeneralKey, fallback: true),
        AnalyzerOptionReader.ReadBool(options, IncludeNonPublicTypesRuleKey, IncludeNonPublicTypesGeneralKey, fallback: true));
}
