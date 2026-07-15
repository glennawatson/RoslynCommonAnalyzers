// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for value-equality fast-path analyzer benchmarks (SST2435).</summary>
internal static class ValueEqualityFastPathBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating equality fast paths.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           """;

    /// <summary>Builds one hierarchy whose derived equality combines the base with '&amp;&amp;'.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public class B{{index}}
           {
               public int Y { get; set; }

               public override bool Equals(object obj) => obj is B{{index}} b && b.Y == Y;

               public override int GetHashCode() => Y;
           }

           public class D{{index}} : B{{index}}
           {
               public int Z { get; set; }

               public override bool Equals(object obj) => base.Equals(obj) && obj is D{{index}} d && d.Z == Z;

               public override int GetHashCode() => Z;
           }
           """;

    /// <summary>Builds one hierarchy whose derived equality uses the base as an early-return-true fast path.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public class VB{{index}}
           {
               public int Y { get; set; }

               public override bool Equals(object obj) => obj is VB{{index}} b && b.Y == Y;

               public override int GetHashCode() => Y;
           }

           public class VD{{index}} : VB{{index}}
           {
               public int Z { get; set; }

               public override bool Equals(object obj)
               {
                   if (base.Equals(obj))
                   {
                       return true;
                   }

                   return obj is VD{{index}} d && d.Z == Z;
               }

               public override int GetHashCode() => Z;
           }
           """;
}
