// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for identity-hash-in-value-hash analyzer benchmarks (SST2481).</summary>
internal static class IdentityHashInValueHashBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating hash overrides.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           """;

    /// <summary>Builds one hierarchy whose derived hash chains a real base value hash.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public class B{{index}}
           {
               private readonly int _y;

               public override bool Equals(object obj) => obj is B{{index}} b && b._y == _y;

               public override int GetHashCode() => _y.GetHashCode();
           }

           public class D{{index}} : B{{index}}
           {
               private readonly int _z;

               public override bool Equals(object obj) => base.Equals(obj) && obj is D{{index}} d && d._z == _z;

               public override int GetHashCode() => base.GetHashCode() ^ _z.GetHashCode();
           }
           """;

    /// <summary>Builds one type over 'object' whose hash folds in the base identity hash.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public class V{{index}}
           {
               private readonly int _a;
               private readonly int _b;

               public override bool Equals(object obj) => obj is V{{index}} v && v._a == _a && v._b == _b;

               public override int GetHashCode() => base.GetHashCode() ^ _a.GetHashCode() ^ _b.GetHashCode();
           }
           """;
}
