// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for redundant-GetType analyzer benchmarks (SST2432).</summary>
internal static class RedundantGetTypeBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating GetType calls.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           """;

    /// <summary>Builds one type whose GetType call is on an ordinary object.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public Type Run(object value) => value.GetType();
           }
           """;

    /// <summary>Builds one type whose GetType call is on a value already typed as Type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public Type Run(Type value) => value.GetType();
           }
           """;
}
