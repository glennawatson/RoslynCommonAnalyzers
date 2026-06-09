// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for using-directive-qualified analyzer benchmarks (SST1135).</summary>
internal static class UsingDirectiveQualifiedBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating using-qualification patterns.</summary>
    /// <param name="types">The number of synthetic containers to emit.</param>
    /// <param name="violating">Whether to emit using-qualification rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => BenchmarkSourceText.JoinBlocks(types, i => GenerateContainer(i, violating));

    /// <summary>Builds one clean or violating using-qualification container.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <param name="violating">Whether to emit a violating container.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateContainer(int index, bool violating)
        => violating ? GenerateViolatingContainer(index) : GenerateCleanContainer(index);

    /// <summary>Builds one container whose nested using is fully qualified.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateCleanContainer(int index)
        => $$"""
           namespace Bench.N{{index}}.Sub
           {
               internal sealed class T{{index}}
               {
               }
           }

           namespace Bench.N{{index}}
           {
               using Bench.N{{index}}.Sub;

               internal sealed class C{{index}}
               {
                   private T{{index}} F() => new();
               }
           }
           """;

    /// <summary>Builds one container whose nested using is written in context-relative form.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateViolatingContainer(int index)
        => $$"""
           namespace Bench.N{{index}}.Sub
           {
               internal sealed class T{{index}}
               {
               }
           }

           namespace Bench.N{{index}}
           {
               using Sub;

               internal sealed class C{{index}}
               {
                   private T{{index}} F() => new();
               }
           }
           """;
}
