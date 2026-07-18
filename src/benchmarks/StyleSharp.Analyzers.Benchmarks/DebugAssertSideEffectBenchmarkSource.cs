// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for Debug.Assert side-effect analyzer benchmarks (SST2450).</summary>
internal static class DebugAssertSideEffectBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating Debug.Assert conditions.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Collections.Generic;
           using System.Diagnostics;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose asserts only read from their arguments.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void Run(List<int> items, int value)
               {
                   Debug.Assert(items != null && items.Count > value);
                   Debug.Assert(items.Contains(value));
               }
           }
           """;

    /// <summary>Builds one type whose asserts mutate their arguments in the condition.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Run(List<int> items, int value)
               {
                   Debug.Assert(items.Remove(value));
                   Debug.Assert(value++ > 0);
               }
           }
           """;
}
