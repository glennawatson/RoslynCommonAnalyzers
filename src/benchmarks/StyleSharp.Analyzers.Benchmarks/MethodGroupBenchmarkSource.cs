// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for method-group analysis.</summary>
internal static class MethodGroupBenchmarkSource
{
    /// <summary>Builds clean or violating lambda members.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit forwarding lambdas.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class MethodGroupBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}

               private static int Square(int value) => value * value;
           }
           """;

    /// <summary>Builds one synthetic member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member source.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating
            ? $"public System.Func<int, int> M{index}() => value => Square(value);"
            : $"public System.Func<int, int> M{index}() => value => Square(value + {index});";
}
