// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for inheritance-depth analyzer benchmarks.</summary>
internal static class InheritanceDepthBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating inheritance-depth patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit inheritance-depth rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           public class D0
           {
           }

           public class D1 : D0
           {
           }

           public class D2 : D1
           {
           }

           public class D3 : D2
           {
           }

           public class D4 : D3
           {
           }

           public class D5 : D4
           {
           }

           public class D6 : D5
           {
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating inheritance-depth type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type three levels deep, below the default maximum.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public class C{{index}} : D2
           {
           }
           """;

    /// <summary>Builds one type seven levels deep, above the default maximum.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class C{{index}} : D6
           {
           }
           """;
}
