// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for repeated-word documentation analyzer (SST1658) benchmarks.</summary>
internal static class RepeatedWordsBenchmarkSource
{
    /// <summary>Builds clean or violating documented members.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit repeated-word violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class RepeatedWordsBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating documented member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating
            ? $$"""
                /// <summary>Gets the the value {{index}} from the store.</summary>
                /// <param name="input">The the input value.</param>
                public void M{{index}}(int input)
                {
                }
                """
            : $$"""
                /// <summary>Gets the value {{index}} from the store.</summary>
                /// <param name="input">The input value.</param>
                public void M{{index}}(int input)
                {
                }
                """;
}
