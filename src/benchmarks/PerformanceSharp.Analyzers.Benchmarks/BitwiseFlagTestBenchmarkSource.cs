// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for bitwise-flag-test analyzer benchmarks.</summary>
internal static class BitwiseFlagTestBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating flag-test patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit HasFlag rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating flag-test type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean flag-test type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           [System.Flags]
           public enum E{{index}}
           {
               None = 0,
               A = 1,
               B = 2,
           }

           public sealed class C{{index}}
           {
               public bool M(E{{index}} value)
                   => (value & E{{index}}.A) == E{{index}}.A;
           }
           """;

    /// <summary>Builds one violating flag-test type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           [System.Flags]
           public enum E{{index}}
           {
               None = 0,
               A = 1,
               B = 2,
           }

           public sealed class C{{index}}
           {
               public bool M(E{{index}} value)
                   => value.HasFlag(E{{index}}.A);
           }
           """;
}
