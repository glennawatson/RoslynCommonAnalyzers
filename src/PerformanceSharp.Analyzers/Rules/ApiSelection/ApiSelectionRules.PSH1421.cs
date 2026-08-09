// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1421 descriptor.</summary>
internal static partial class ApiSelectionRules
{
    /// <summary>PSH1421 — a static Regex call inside a loop re-resolves its pattern on every iteration.</summary>
    public static readonly DiagnosticDescriptor CacheRegexOutsideLoop = Create(
        "PSH1421",
        "Hoist a Regex out of the loop that calls it",
        "'Regex.{0}' is called on every iteration; hold the pattern in a cached instance outside the loop",
        CacheRegexOutsideLoopDescription);

    /// <summary>The PSH1421 rule description.</summary>
    private const string CacheRegexOutsideLoopDescription =
        "The static 'Regex' methods look up the pattern in a process-wide cache on every call, hash the pattern string to do "
        + "it, and take a lock on the cache while they do. That is per-call overhead a loop pays once per iteration, and the "
        + "cache is bounded — 'Regex.CacheSize' defaults to fifteen entries — so a program using more patterns than that "
        + "evicts and fully re-parses the pattern each time round, turning a lookup into a compile. A single instance built "
        + "once outside the loop resolves the pattern once, can be given 'RegexOptions.Compiled' when the loop is hot, and "
        + "on .NET 7 and later can be a source-generated partial property that costs nothing at run time at all. Reported for "
        + "a static 'Regex' call written inside a 'for', 'foreach', 'while', or 'do' body; an instance call already holds its "
        + "pattern and is never reported, and the whole rule stays silent when 'Regex' does not resolve in the compilation.";
}
