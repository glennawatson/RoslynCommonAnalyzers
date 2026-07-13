// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for span-based concatenation analyzer benchmarks.</summary>
/// <remarks>
/// Every violating type carries exactly one PSH1222, and every clean type carries none, so the
/// diagnostic count for a corpus of N types is N and 0 respectively.
/// </remarks>
internal static class UseSpanBasedConcatBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating span-based concatenation code.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type, written the way the rule is asking for.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string M(string a, string b) => string.Concat(a.AsSpan(1), b.AsSpan());
           }
           """;

    /// <summary>Builds one violating type, carrying exactly one PSH1222.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string M(string a, string b) => string.Concat(a.Substring(1), b);
           }
           """;
}
