// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for captured-loop-variable analyzer benchmarks (SST2479).</summary>
internal static class CapturedLoopVariableBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating loop captures.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose loop delegates run in place or capture a stable local.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void InPlace(List<int> items)
               {
                   for (int i = 0; i < items.Count; i++)
                       items.ForEach(x => Use(i + x));
               }

               public void Stable(List<Action> handlers)
               {
                   int x = 42;
                   for (int i = 0; i < 3; i++)
                       handlers.Add(() => Use(x));
               }

               private void Use(int value) { }
           }
           """;

    /// <summary>Builds one type whose loop delegate captures the for variable and escapes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Escaping(List<Action> handlers)
               {
                   for (int i = 0; i < 3; i++)
                       handlers.Add(() => Use(i));
               }

               private void Use(int value) { }
           }
           """;
}
