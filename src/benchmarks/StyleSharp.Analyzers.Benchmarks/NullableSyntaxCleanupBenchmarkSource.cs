// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for nullable syntax cleanup analysis.</summary>
internal static class NullableSyntaxCleanupBenchmarkSource
{
    /// <summary>Builds clean or violating nullable cleanup source.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable nullable syntax.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => violating
            ? $$"""
               #nullable enable
               #nullable enable

               namespace Bench;

               internal sealed class NullableSyntaxCleanupBench
               {
               {{BenchmarkSourceText.JoinBlocks(members, GenerateViolatingMember)}}
               }
               """
            : $$"""
               #nullable enable

               namespace Bench;

               internal sealed class NullableSyntaxCleanupBench
               {
               {{BenchmarkSourceText.JoinBlocks(members, GenerateCleanMember)}}
               }
               """;

    /// <summary>Builds a violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           public int Value{{index}}()
           {
               var value = {{index}};
               return value!;
           }
           """;

    /// <summary>Builds a clean member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => $$"""
           public string Value{{index}}(string? value) => value!;
           """;
}
