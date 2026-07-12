// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for seal-non-derived-type analyzer benchmarks.</summary>
/// <remarks>
/// The corpus carries no <c>[InternalsVisibleTo]</c>, so its internal classes stay reportable. Every
/// violating type carries exactly one PSH1411 and every clean type carries none, so the diagnostic
/// count for a corpus of N types is N and 0 respectively.
/// </remarks>
internal static class SealNonDerivedTypeBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating class declarations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating
            ? $$"""
              internal class C{{index}}
              {
                  public int M() => {{index}};
              }
              """
            : $$"""
              internal sealed class C{{index}}
              {
                  public int M() => {{index}};
              }
              """;
}
