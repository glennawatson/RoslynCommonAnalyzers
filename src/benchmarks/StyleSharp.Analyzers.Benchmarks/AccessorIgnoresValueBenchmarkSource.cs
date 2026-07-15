// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for accessor-ignores-value analyzer benchmarks (SST2429).</summary>
internal static class AccessorIgnoresValueBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating write accessors.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose setter reads the value it is handed.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public class C{{index}}
           {
               private int _h{{index}};

               public int Height{{index}} { get => _h{{index}}; set => _h{{index}} = value; }
           }
           """;

    /// <summary>Builds one type whose setter reads the wrong field and never reads value.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class V{{index}}
           {
               private int _h{{index}};
               private int _height{{index}};
               private int _width{{index}};

               public int Height{{index}} { get => _h{{index}}; set => _height{{index}} = _width{{index}}; }
           }
           """;
}
