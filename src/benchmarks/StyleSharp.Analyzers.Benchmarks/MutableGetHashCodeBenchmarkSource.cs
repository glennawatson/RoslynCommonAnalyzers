// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for mutable-GetHashCode analyzer benchmarks.</summary>
internal static class MutableGetHashCodeBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating hash overrides.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit mutable-GetHashCode rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating hash-override type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose hash reads only state that construction fixes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a const, a static readonly, a readonly
    /// field, an <c>init</c>-only property, a get-only property, a local, and a read through another object.
    /// The type also carries a mutable field and an ordinary method, so the syntactic prepass has real
    /// members to reject before it reaches the one hash override.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class Clean{{index}}
           {
               private const int Seed = 17;

               private static readonly int Multiplier = 31;

               private readonly int _id;

               private readonly int[] _tags;

               private int _visits;

               public Clean{{index}}(int id, int[] tags)
               {
                   _id = id;
                   _tags = tags;
                   _visits = 0;
               }

               public int Version { get; init; }

               public int Depth { get; }

               public int Visit() => ++_visits;

               public override int GetHashCode()
               {
                   var hash = Seed;
                   hash = (hash * Multiplier) + _id;
                   hash = (hash * Multiplier) + _tags.Length;
                   hash = (hash * Multiplier) + Version;
                   return hash + Depth;
               }
           }
           """;

    /// <summary>Builds one type whose hash reads four members that can change afterwards.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class Dirty{{index}}
           {
               private int _id;

               private int _kind;

               public Dirty{{index}}(int id, int kind)
               {
                   _id = id;
                   _kind = kind;
               }

               public int Version { get; set; }

               public int Depth { get; set; }

               public void Renumber(int id) => _id = id;

               public override int GetHashCode()
               {
                   var hash = 17;
                   hash = (hash * 31) + _id;
                   hash = (hash * 31) + _kind;
                   hash = (hash * 31) + Version;
                   return hash + this.Depth;
               }
           }
           """;
}
