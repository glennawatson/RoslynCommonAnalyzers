// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST2438 level floor for one syntax tree.</summary>
/// <param name="Floor">The lowest level, as its ordinal, at which a discarded exception is reported.</param>
/// <remarks>
/// Levels are ordered Trace, Debug, Information, Warning, Error, Critical, matching the runtime enum, so a
/// higher ordinal is a more severe level. The floor defaults to error, the level at which a lost stack trace
/// is a real operational problem; lowering it opts the noisier levels in.
/// </remarks>
internal readonly record struct LogLevelFloorOptions(int Floor)
{
    /// <summary>The Trace ordinal.</summary>
    public const int Trace = 0;

    /// <summary>The Debug ordinal.</summary>
    public const int Debug = 1;

    /// <summary>The Information ordinal.</summary>
    public const int Information = 2;

    /// <summary>The Warning ordinal.</summary>
    public const int Warning = 3;

    /// <summary>The Error ordinal, and the default floor.</summary>
    public const int Error = 4;

    /// <summary>The Critical ordinal, and the highest reportable level.</summary>
    public const int Critical = 5;

    /// <summary>The sentinel for a name that is not a known level.</summary>
    private const int Unknown = -1;

    /// <summary>The rule-specific minimum-level key.</summary>
    private const string MinimumLevelRuleKey = "stylesharp.SST2438.minimum_level";

    /// <summary>Reads the level floor for one tree, falling back to error.</summary>
    /// <param name="options">The analyzer config options for the call's tree.</param>
    /// <returns>The resolved floor.</returns>
    public static LogLevelFloorOptions Read(AnalyzerConfigOptions options)
        => new(options.TryGetValue(MinimumLevelRuleKey, out var value) && TryParseLevel(value, out var level) ? level : Error);

    /// <summary>Returns whether a call's level is at or above the floor and within the reportable range.</summary>
    /// <param name="level">The call's level ordinal.</param>
    /// <returns><see langword="true"/> when the level is reportable.</returns>
    public bool Includes(int level) => level >= Floor && level <= Critical;

    /// <summary>Parses a level name to its ordinal.</summary>
    /// <param name="value">The configured level name.</param>
    /// <param name="level">The parsed ordinal.</param>
    /// <returns><see langword="true"/> when the name is a known level.</returns>
    private static bool TryParseLevel(string value, out int level)
    {
        var parsed = value.Trim().ToLowerInvariant() switch
        {
            "trace" => Trace,
            "debug" => Debug,
            "information" => Information,
            "warning" => Warning,
            "error" => Error,
            "critical" => Critical,
            _ => Unknown,
        };

        var known = parsed != Unknown;
        level = known ? parsed : Error;
        return known;
    }
}
