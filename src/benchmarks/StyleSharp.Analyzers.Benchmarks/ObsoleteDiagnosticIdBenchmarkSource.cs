// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for obsolete-without-diagnostic-id analysis (SST2314).</summary>
internal static class ObsoleteDiagnosticIdBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises identified or unidentified deprecations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    /// <remarks>
    /// The corpus is compiled against the host runtime, whose <c>ObsoleteAttribute</c> has a
    /// <c>DiagnosticId</c> — so the rule's compilation-start gate opens and the syntax callback really runs.
    /// On a target without the property the analyzer registers nothing, and there is no per-node path to
    /// measure at all.
    /// </remarks>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Diagnostics;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose deprecations a caller can act on, or which the rule does not own.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route: an unrelated attribute (the name test), a deprecation carrying an id,
    /// one carrying an id and a url, and a bare attribute with no message — which belongs to SST2308 and
    /// which this rule has to hand over rather than report.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           [DebuggerDisplay("C{{index}}")]
           public sealed class C{{index}}
           {
               [Obsolete("Use Value instead.", DiagnosticId = "BENCH{{index}}")]
               public int Legacy { get; set; }

               [Obsolete("Use Run(RunOptions).", DiagnosticId = "BENCH001", UrlFormat = "https://bench.dev/{0}")]
               public void Identified()
               {
               }

               [Obsolete]
               public void LeftToSst2308()
               {
               }

               [Conditional("DEBUG")]
               public void Traced()
               {
               }
           }
           """;

    /// <summary>Builds one type whose deprecations explain themselves but cannot be suppressed on their own.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           [Obsolete("Use W{{index}} instead.")]
           public sealed class V{{index}}
           {
               [Obsolete("Use Value instead.")]
               public int Legacy { get; set; }

               [Obsolete("Use Run(RunOptions) instead.", true)]
               public void Fatal()
               {
               }

               [Obsolete("Use Run(RunOptions).", UrlFormat = "https://bench.dev/{0}")]
               public void UrlWithoutId()
               {
               }
           }
           """;
}
