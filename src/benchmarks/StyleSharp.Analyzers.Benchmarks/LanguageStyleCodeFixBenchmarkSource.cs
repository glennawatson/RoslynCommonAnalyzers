// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds source for grouped language-style code-fix benchmarks.</summary>
internal static class LanguageStyleCodeFixBenchmarkSource
{
    /// <summary>Builds repeated null-coalescing candidates.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members)
        => $$"""
           namespace Bench;

           internal sealed class LanguageStyleCodeFixBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, static i => GenerateMember(i))}}
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index)
        => $$"""
           public string Coalesce{{index}}(string? value, string fallback) => value == null ? fallback : value;
           """;
}
