// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for in-parameter-with-non-readonly-struct analyzer benchmarks.</summary>
internal static class InParameterWithNonReadonlyStructBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating <c>in</c>-parameter patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit in-parameter rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating in-parameter type pair.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type pair.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean in-parameter type pair.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public readonly struct S{{index}}
           {
               public int Value { get; }
           }

           public static class C{{index}}
           {
               public static int M(in S{{index}} value) => value.Value;
           }
           """;

    /// <summary>Builds one violating in-parameter type pair.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public struct S{{index}}
           {
               public int Value;
           }

           public static class C{{index}}
           {
               public static int M(in S{{index}} value) => value.Value;
           }
           """;
}
