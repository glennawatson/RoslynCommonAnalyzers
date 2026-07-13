// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for unused-local analyzer benchmarks.</summary>
internal static class UnusedLocalBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating local declarations.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit unused locals.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Collections.Generic;
           using System.IO;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose locals are all read, or are shapes the rule never registers.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers the reads the scan has to find — a direct read, a read through a compound assignment, a read
    /// from inside a lambda — and the declarations that never reach it: a discard, a using declaration, a
    /// foreach variable, a pattern variable, and an out variable the caller goes on to read.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Read(int value)
               {
                   var doubled = value * 2;
                   return doubled;
               }

               public int Accumulate(List<int> values)
               {
                   var total = 0;
                   foreach (var value in values)
                   {
                       total += value;
                   }

                   return total;
               }

               public Func<int> Capture(int value)
               {
                   var captured = value;
                   return () => captured;
               }

               public int Parse(string text, object candidate)
               {
                   var _ = text.Length;
                   using var stream = new MemoryStream();
                   if (candidate is string other)
                   {
                       return other.Length;
                   }

                   return int.TryParse(text, out var parsed) ? parsed : 0;
               }
           }
           """;

    /// <summary>Builds one type whose locals are declared and never read.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Emits four reported locals: a pure initializer, a call, a write-only local, and an out variable.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Compute() => 1;

               public int Pure(int value)
               {
                   int unused = value + 1;
                   return value;
               }

               public void Call()
               {
                   var result = Compute();
               }

               public void WriteOnly(bool flag)
               {
                   int state = 0;
                   if (flag)
                   {
                       state = 1;
                   }
               }

               public bool Out(string text) => int.TryParse(text, out var parsed);
           }
           """;
}
