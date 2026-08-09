// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The PSH1421 descriptor.</summary>
internal static partial class ApiSelectionRules
{
    /// <summary>The message fragment for a call made from inside a loop body.</summary>
    public const string RegexCalledPerIteration = "re-resolves its pattern on every iteration";

    /// <summary>The message fragment for a call whose pattern is a compile-time constant.</summary>
    public const string RegexConstantPattern = "re-resolves a constant pattern on every call";

    /// <summary>PSH1421 — a static Regex call re-resolves its pattern through the process-wide cache on every call.</summary>
    public static readonly DiagnosticDescriptor CacheRegexOutsideLoop = Create(
        "PSH1421",
        "Hold a Regex in a cached instance instead of calling the static overload",
        "'Regex.{0}' {1}; hold the pattern in a cached instance",
        CacheRegexOutsideLoopDescription);

    /// <summary>The PSH1421 rule description.</summary>
    private const string CacheRegexOutsideLoopDescription =
        "The static 'Regex' methods look up the pattern in a process-wide cache on every call, hash the pattern string to do "
        + "it, and take a lock on the cache while they do. That is per-call overhead a loop pays once per iteration, and the "
        + "cache is bounded — 'Regex.CacheSize' defaults to fifteen entries — so a program using more patterns than that "
        + "evicts and fully re-parses the pattern each time round, turning a lookup into a compile. A single instance built "
        + "once resolves the pattern once, can be given 'RegexOptions.Compiled' when the call site is hot, and on .NET 7 and "
        + "later can be a source-generated partial member that costs nothing at run time at all. Two shapes are reported: a "
        + "static call written inside a 'for', 'foreach', 'while', or 'do' body, where the lookup is paid once per iteration; "
        + "and a static call whose pattern argument is a compile-time constant, which can always be hoisted whether or not a "
        + "loop surrounds it. A pattern built at run time outside a loop is left alone — there is nothing constant to hoist. "
        + "An instance call already holds its pattern and is never reported, and the whole rule stays silent when 'Regex' does "
        + "not resolve in the compilation.";
}
