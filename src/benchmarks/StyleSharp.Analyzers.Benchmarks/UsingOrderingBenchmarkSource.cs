// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for using-ordering analyzer benchmarks.</summary>
internal static class UsingOrderingBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating using-ordering patterns.</summary>
    /// <param name="containers">The number of synthetic containers to emit.</param>
    /// <param name="violating">Whether to emit using-ordering rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int containers, bool violating)
        => violating
            ? BenchmarkSourceText.JoinBlocks(containers, GenerateViolatingNamespace)
            : $$"""
               using System;
               {{BenchmarkSourceText.JoinLines(containers, static i => $"using N{i:0000};")}}

               namespace Bench;

               {{BenchmarkSourceText.JoinBlocks(containers, GenerateCleanNamespaceMember)}}
               """;

    /// <summary>Builds one clean type declaration for the using-ordering benchmark.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanNamespaceMember(int index)
        => $$"""
           public static class C{{index}}
           {
           }
           """;

    /// <summary>Builds one violating namespace block for the using-ordering benchmark.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated namespace block.</returns>
    private static string GenerateViolatingNamespace(int index)
        => $$"""
           namespace Bench{{index}}
           {
               using Alias{{index}} = System.Console;
               using static System.Math;
               using N{{index:0000}};
               using System;

               public static class C{{index}}
               {
               }
           }
           """;
}
