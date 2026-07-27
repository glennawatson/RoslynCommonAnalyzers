// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the anonymous-type-to-tuple benchmarks (PSH1023).</summary>
internal static class PreferTupleOverAnonymousTypeBenchmarkSource
{
    /// <summary>Builds a compilation unit whose locals are tuples, or anonymous types that could be.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one type holding a local pair, plus an anonymous type that escapes either way.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether the local pair is an anonymous type.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The escaping local stays in both corpora so the measured cost always includes the escape scan
    /// rejecting a candidate, which is the work the clean path actually does.
    /// </remarks>
    private static string GenerateType(int index, bool violating)
        => $$"""
           public class Case{{index}}
           {
               public object Escaping{{index}}()
               {
                   var payload = new { Left = {{index}}, Right = {{index}} };
                   return payload;
               }

               public int Local{{index}}()
               {
                   var pair = {{(violating ? $"new {{ Left = {index}, Right = {index} }}" : $"(Left: {index}, Right: {index})")}};
                   return pair.Left + pair.Right;
               }
           }
           """;
}
