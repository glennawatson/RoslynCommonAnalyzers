// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for documentation-text analyzer benchmarks.</summary>
internal static class DocumentationTextBenchmarkSource
{
    /// <summary>Builds a compilation unit containing many documentation comments.</summary>
    /// <param name="members">The number of documented methods to emit.</param>
    /// <param name="violating">Whether to emit low-quality summary text.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
    {
        var summary = violating ? "singleword" : "Does the work here.";
        return $$"""
               namespace Bench;
               internal sealed class DocumentationTextBench
               {
               {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, summary))}}
               }
               """;
    }

    /// <summary>Builds one documented member for the benchmark corpus.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="summary">The summary text to emit.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, string summary)
        => $$"""
           /// <summary>{{summary}}</summary>
           internal void M{{index}}()
           {
           }
           """;
}
