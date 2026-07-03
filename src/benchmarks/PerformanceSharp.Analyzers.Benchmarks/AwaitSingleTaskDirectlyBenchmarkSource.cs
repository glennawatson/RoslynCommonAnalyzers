// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for await-single-task-directly analyzer benchmarks.</summary>
internal static class AwaitSingleTaskDirectlyBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating single-task combinator patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit await-single-task-directly rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Threading.Tasks;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating task-combinator type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean task-combinator type that coordinates multiple tasks.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async Task M()
               {
                   var first = DoWorkAsync();
                   var second = DoWorkAsync();
                   await Task.WhenAll(first, second);
               }

               private static async Task DoWorkAsync() => await Task.Yield();
           }
           """;

    /// <summary>Builds one violating type that wraps a single task in WhenAll.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async Task M()
               {
                   var single = DoWorkAsync();
                   await Task.WhenAll(single);
               }

               private static async Task DoWorkAsync() => await Task.Yield();
           }
           """;
}
