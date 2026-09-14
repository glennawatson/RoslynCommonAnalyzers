// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1484 settings for one syntax tree.</summary>
/// <param name="CheckBaseTypes">Whether a field that hides an inherited field of the same name is reported.</param>
internal readonly record struct ShadowedDeclarationOptions(bool CheckBaseTypes) : ITreeOptions<ShadowedDeclarationOptions>
{
    /// <summary>The rule-specific base-type key.</summary>
    private const string CheckBaseTypesRuleKey = "stylesharp.SST1484.check_base_types";

    /// <summary>The project-wide base-type key.</summary>
    private const string CheckBaseTypesGeneralKey = "stylesharp.check_base_types";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the declaration's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// The base-type check stays off unless it is switched on explicitly: hiding an inherited field with a
    /// field of the same name is sometimes deliberate, so an unset or unparsable value leaves it off rather
    /// than letting a typo light up a whole hierarchy.
    /// </remarks>
    internal static ShadowedDeclarationOptions Read(AnalyzerConfigOptions options) =>
        new(AnalyzerOptionReader.ReadBool(options, CheckBaseTypesRuleKey, CheckBaseTypesGeneralKey, fallback: false));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ShadowedDeclarationOptions ITreeOptions<ShadowedDeclarationOptions>.ReadFrom(AnalyzerConfigOptions options) => Read(options);
}
