// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for large-readonly-struct in-parameter analyzer benchmarks.</summary>
internal static class PassLargeReadonlyStructByInBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating by-value struct parameters.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;
           using System.Threading;
           using System.Threading.Tasks;

           namespace Bench;

           public readonly struct Snapshot
           {
               public readonly long A;
               public readonly long B;
               public readonly long C;
               public readonly long D;
               public readonly long E;
           }

           public readonly struct Small
           {
               public readonly long A;
               public readonly long B;
           }

           public struct Mutable
           {
               public long A;
               public long B;
               public long C;
               public long D;
               public long E;
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating block.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating block.</param>
    /// <returns>The generated block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one block whose every parameter is rejected without a diagnostic.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated block.</returns>
    /// <remarks>
    /// Covers each rejection route in the order the analyzer takes them: a built-in type keyword and an
    /// existing modifier are rejected on syntax alone; a class, a small struct and a mutable struct are
    /// rejected on the bound type; an excluded framework type and a public signature are rejected on the
    /// settings; and an async method, an iterator, a captured parameter and a written one are rejected only
    /// after the body walk.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           internal static class C{{index}}
           {
               internal static int Primitive(int value, string text) => value + text.Length;

               internal static long Already(in Snapshot snapshot) => snapshot.A;

               internal static long Reference(object value) => value.GetHashCode();

               internal static long Register(Small small) => small.A;

               internal static long Defensive(Mutable mutable) => mutable.A;

               internal static bool Framework(CancellationToken token) => token.IsCancellationRequested;

               internal static async Task<long> ScoreAsync(Snapshot snapshot)
               {
                   await Task.Yield();
                   return snapshot.A;
               }

               internal static IEnumerable<long> Enumerate(Snapshot snapshot)
               {
                   yield return snapshot.A;
               }

               internal static Func<long> Capture(Snapshot snapshot) => () => snapshot.B;

               internal static long Written(Snapshot snapshot)
               {
                   snapshot = default;
                   return snapshot.C;
               }
           }

           public static class P{{index}}
           {
               public static long Exposed(Snapshot snapshot) => snapshot.A;
           }
           """;

    /// <summary>Builds one block whose by-value struct parameters are all reported.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated block.</returns>
    /// <remarks>Four diagnostics per block: three methods and the constructor.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           internal sealed class V{{index}}
           {
               internal V{{index}}(Snapshot snapshot) => Total = snapshot.A;

               internal long Total { get; }

               internal static long Score(Snapshot snapshot) => snapshot.A;

               internal static long Rank(Snapshot snapshot) => snapshot.B;

               internal static long Weigh(Snapshot snapshot) => snapshot.C;
           }
           """;
}
