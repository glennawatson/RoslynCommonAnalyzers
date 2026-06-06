// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for expression-heavy analyzer benchmarks.</summary>
internal static class ExpressionHotspotBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating expression patterns.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit violating patterns.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class ExpressionBench(int captured)
           {
               private int _state = captured;

               private void Helper(int value)
               {
           {{(violating ? "        _state = value;" : "        this._state = value;")}}
               }

               private static void Mutate(ref int value)
               {
                   value++;
               }

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violation.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating ? GenerateViolatingMember(index) : GenerateCleanMember(index);

    /// <summary>Builds one clean member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => $$"""
           internal int M{{index}}(int value, int other)
           {
               this.Helper(value);
               return (value + other) << 1;
           }

           internal int Read{{index}}()
           {
               return captured + {{index}};
           }
           """;

    /// <summary>Builds one violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           internal int M{{index}}(int value, int other)
           {
               Helper(value);
               captured = value;
               captured++;
               Mutate(ref captured);
               return value + other << 1;
           }
           """;
}
