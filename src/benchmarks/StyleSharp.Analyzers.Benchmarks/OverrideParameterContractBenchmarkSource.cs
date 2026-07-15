// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for override-parameter-contract analyzer benchmarks (SST2424, SST2426).</summary>
internal static class OverrideParameterContractBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating override parameter lists.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           """;

    /// <summary>Builds one hierarchy whose override repeats the base's default and params modifier.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public class Base{{index}}
           {
               public virtual int Go(int a, int b = 1) => b;
           }

           public class Derived{{index}} : Base{{index}}
           {
               public override int Go(int a, int b = 1) => b;
           }
           """;

    /// <summary>Builds one hierarchy whose override changes the base's default.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public class VBase{{index}}
           {
               public virtual int Go(int a, int b = 1) => b;
           }

           public class VDerived{{index}} : VBase{{index}}
           {
               public override int Go(int a, int b = 2) => b;
           }
           """;
}
