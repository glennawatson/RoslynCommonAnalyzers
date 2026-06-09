// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1504 accessor-consistency analyzer benchmarks.</summary>
internal static class Sst1504AccessorConsistencyBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating accessor-consistency patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit accessor-consistency rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating accessor-consistency type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type whose block-bodied accessors are all single-line.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private int _value{{index}};

               public int Value
               {
                   get { return _value{{index}}; }
                   set { _value{{index}} = value; }
               }
           }
           """;

    /// <summary>Builds one violating type that mixes a single-line and a multi-line block accessor.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private int _value{{index}};

               public int Value
               {
                   get { return _value{{index}}; }
                   set
                   {
                       _value{{index}} = value;
                   }
               }
           }
           """;
}
