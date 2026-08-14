// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2266 settings for one syntax tree.</summary>
/// <param name="MaxInitializerLength">The widest initializer, in characters, still worth inlining.</param>
internal readonly record struct InlineSingleUseLocalOptions(int MaxInitializerLength)
{
    /// <summary>The default maximum initializer width, in characters.</summary>
    public const int DefaultMaxInitializerLength = 40;

    /// <summary>The rule-specific maximum key.</summary>
    private const string MaxRuleKey = "stylesharp.SST2266.max_initializer_length";

    /// <summary>The project-wide maximum key.</summary>
    private const string MaxGeneralKey = "stylesharp.max_initializer_length";

    /// <summary>Reads the settings for one tree, falling back to the default.</summary>
    /// <param name="options">The analyzer config options for the declaration's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// Inlining trades a name for the expression behind it, and past a certain width the expression is the
    /// harder read. The threshold matters most where inlining one local leaves the one before it single-use
    /// too: each round is offered on its own, so a chain of wide initializers can collapse into a single
    /// expression that repeats them. Keeping the rule to initializers short enough that the name was not
    /// earning its place stops that before it starts. An unset, non-numeric, or non-positive value keeps the
    /// default, so a typo neither disables the rule nor lets every width through.
    /// </remarks>
    public static InlineSingleUseLocalOptions Read(AnalyzerConfigOptions options)
        => new(ReadPositiveInt(options, MaxRuleKey, MaxGeneralKey, DefaultMaxInitializerLength));

    /// <summary>Reads a positive integer setting, preferring the rule-specific key.</summary>
    /// <param name="options">The analyzer config options.</param>
    /// <param name="ruleKey">The rule-specific key.</param>
    /// <param name="generalKey">The project-wide key.</param>
    /// <param name="fallback">The value used when neither key parses.</param>
    /// <returns>The configured positive integer, or <paramref name="fallback"/>.</returns>
    private static int ReadPositiveInt(AnalyzerConfigOptions options, string ruleKey, string generalKey, int fallback)
    {
        if (options.TryGetValue(ruleKey, out var value) && int.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return options.TryGetValue(generalKey, out value) && int.TryParse(value, out parsed) && parsed > 0
            ? parsed
            : fallback;
    }
}
