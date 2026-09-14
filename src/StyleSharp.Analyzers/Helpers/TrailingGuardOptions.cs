// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2273 settings for one syntax tree.</summary>
/// <param name="MinWrappedStatements">The fewest statements a trailing <c>if</c> must wrap to be reported.</param>
internal readonly record struct TrailingGuardOptions(int MinWrappedStatements)
{
    /// <summary>The default minimum wrapped-statement count.</summary>
    public const int DefaultMinWrappedStatements = 2;

    /// <summary>The rule-specific minimum key.</summary>
    private const string MinRuleKey = "stylesharp.SST2273.min_wrapped_statements";

    /// <summary>The project-wide minimum key.</summary>
    private const string MinGeneralKey = "stylesharp.min_wrapped_statements";

    /// <summary>Reads the settings for one tree, falling back to the default.</summary>
    /// <param name="options">The analyzer config options for the declaration's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// A trailing one-liner such as <c>if (x) Do();</c> is left alone by default: inverting it trades no
    /// nesting for an extra jump. The threshold keeps the rule to bodies where flattening actually removes a
    /// level of indentation. An unset, non-numeric, or non-positive value keeps the default, so a typo neither
    /// disables the rule nor fires it on every single-statement <c>if</c>.
    /// </remarks>
    internal static TrailingGuardOptions Read(AnalyzerConfigOptions options) =>
        new(AnalyzerOptionReader.ReadPositiveInt(options, MinRuleKey, MinGeneralKey, DefaultMinWrappedStatements));
}
