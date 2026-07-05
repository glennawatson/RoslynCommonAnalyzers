// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for file-scoped-namespace analysis.</summary>
internal static class FileScopedNamespaceBenchmarkSource
{
    /// <summary>Builds clean or violating namespace source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit a block-scoped namespace.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => violating
            ? $$"""
               namespace Bench
               {
                   internal sealed class FileScopedNamespaceBench
                   {
               {{BenchmarkSourceText.JoinBlocks(members, GenerateMember)}}
                   }
               }
               """
            : $$"""
               namespace Bench;

               internal sealed class FileScopedNamespaceBench
               {
               {{BenchmarkSourceText.JoinBlocks(members, GenerateMember)}}
               }
               """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateMember(int index) => $"public int M{index}() => {index};";
}
