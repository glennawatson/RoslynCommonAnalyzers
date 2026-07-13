// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for mutable-static-field analyzer benchmarks (SST1499).</summary>
internal static class MutableStaticFieldBenchmarkSource
{
    /// <summary>The number of exposed mutable static fields each violating type declares.</summary>
    public const int ViolationsPerType = 4;

    /// <summary>Builds a compilation unit that exercises clean or violating static state.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to expose mutable static state.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose static state nobody outside it can change.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a constant, an instance field, a private
    /// static field, a per-thread field, and a readonly field of a type whose contents cannot be rewritten.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public const int Limit = 90;

               public static readonly string Name = "bench";

               public static readonly int Retries = 3;

               [ThreadStatic]
               public static int Current;

               private static int _counter;

               public int Total;

               public int Next() => ++_counter + Current + Total;
           }
           """;

    /// <summary>Builds one type that exposes mutable static state.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Each type carries exactly <see cref="ViolationsPerType"/> exposed mutable static fields.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public static int Timeout = 30;

               internal static List<int> Pending = new List<int>();

               public static readonly int[] Weights = new int[4];

               public static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();
           }
           """;
}
