// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for backing-field-mismatch analyzer benchmarks (SST2422).</summary>
internal static class BackingFieldMismatchBenchmarkSource
{
    /// <summary>Builds a compilation unit whose properties round-trip one field or two.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose getter and setter share a single field.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private int _width;

               public int Width
               {
                   get => _width;
                   set => _width = value;
               }
           }
           """;

    /// <summary>Builds one type whose getter reads a different field than its setter writes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private int _width;
               private int _height;

               public int Width
               {
                   get => _height;
                   set => _width = value;
               }
           }
           """;
}
