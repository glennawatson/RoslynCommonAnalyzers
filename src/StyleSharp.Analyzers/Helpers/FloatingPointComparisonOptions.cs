// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1473 settings for one syntax tree.</summary>
/// <param name="AllowZeroComparison">Whether a comparison against a literal zero is left alone.</param>
/// <param name="AllowEqualityMemberComparison">Whether a comparison inside an equality member is left alone.</param>
internal readonly record struct FloatingPointComparisonOptions(bool AllowZeroComparison, bool AllowEqualityMemberComparison) : ITreeOptions<FloatingPointComparisonOptions>
{
    /// <summary>Zero comparisons are allowed unless the configuration says otherwise.</summary>
    public const bool DefaultAllowZeroComparison = true;

    /// <summary>The rule-specific zero-comparison key.</summary>
    private const string AllowZeroRuleKey = "stylesharp.SST1473.allow_zero_comparison";

    /// <summary>The project-wide zero-comparison key.</summary>
    private const string AllowZeroGeneralKey = "stylesharp.allow_zero_comparison";

    /// <summary>The rule-specific equality-member key.</summary>
    private const string AllowEqualityMemberRuleKey = "stylesharp.SST1473.allow_equality_member_comparison";

    /// <summary>The project-wide equality-member key.</summary>
    private const string AllowEqualityMemberGeneralKey = "stylesharp.allow_equality_member_comparison";

    /// <summary>Reads the settings for one tree, falling back to the defaults.</summary>
    /// <param name="options">The analyzer config options for the comparison's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// An unset or unparsable value yields the default, so a typo neither turns the rule off nor starts
    /// reporting every <c>x == 0</c> in the file. The equality-member relaxation defaults to off: an exact
    /// comparison there is still an exact comparison, and the author should decide it deliberately.
    /// </remarks>
    internal static FloatingPointComparisonOptions Read(AnalyzerConfigOptions options) =>
        new(
            AnalyzerOptionReader.ReadBool(options, AllowZeroRuleKey, AllowZeroGeneralKey, DefaultAllowZeroComparison),
            AnalyzerOptionReader.ReadBool(options, AllowEqualityMemberRuleKey, AllowEqualityMemberGeneralKey, fallback: false));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    FloatingPointComparisonOptions ITreeOptions<FloatingPointComparisonOptions>.ReadFrom(AnalyzerConfigOptions options) => Read(options);
}
