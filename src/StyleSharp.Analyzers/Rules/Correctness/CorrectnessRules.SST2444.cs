// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2444 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2444 — a constant regular-expression pattern does not parse.</summary>
    public static readonly DiagnosticDescriptor InvalidRegexPattern = Create(
        "SST2444",
        "A regular expression pattern should be valid",
        "Invalid regular expression: {0}",
        InvalidRegexPatternDescription);

    /// <summary>The InvalidRegexPattern rule description.</summary>
    private const string InvalidRegexPatternDescription =
        "A regular expression built from a constant pattern that does not parse throws the moment the pattern is first used, not at "
        + "build time, so a stray bracket or a bad backreference ships and fails in production on the first input that reaches it. The "
        + "pattern is validated by constructing the real engine with the real options the call passes — an unclosed character class, a "
        + "reversed quantifier, or a backreference the pattern never defines is reported with the engine's own message. Where the "
        + "framework supports it, moving the pattern to a source-generated regular expression is better still: the pattern is validated "
        + "by the compiler, and the matcher is generated ahead of time rather than parsed at run time.";
}
