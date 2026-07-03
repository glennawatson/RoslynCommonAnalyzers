// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for value-type-equality-boxes analyzer benchmarks.</summary>
internal static class ValueTypeEqualityBoxesBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating struct equality patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit value-type-equality rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating struct type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean struct type with full equality members.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public struct S{{index}} : System.IEquatable<S{{index}}>
           {
               public int Value;

               public bool Equals(S{{index}} other) => Value == other.Value;

               public override bool Equals(object obj) => obj is S{{index}} other && Equals(other);

               public override int GetHashCode() => Value;
           }
           """;

    /// <summary>Builds one violating struct type without equality members.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public struct S{{index}}
           {
               public int Value;

               public int M() => Value;
           }
           """;
}
