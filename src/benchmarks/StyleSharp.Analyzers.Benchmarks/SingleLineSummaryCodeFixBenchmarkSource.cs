// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for SST1653 code-fix benchmarks.</summary>
internal static class SingleLineSummaryCodeFixBenchmarkSource
{
    /// <summary>Generates a compilation unit containing many short multi-line summaries.</summary>
    /// <param name="types">The number of documented types to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, GenerateType)}}
           """;

    /// <summary>Builds one violating type declaration.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index)
        => $$"""
           /// <summary>
           /// Short summary {{index}}.
           /// </summary>
           public sealed class C{{index}}
           {
           }
           """;
}
