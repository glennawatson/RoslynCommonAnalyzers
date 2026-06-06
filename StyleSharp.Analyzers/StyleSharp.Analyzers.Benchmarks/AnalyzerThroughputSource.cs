// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the end-to-end analyzer throughput benchmark.</summary>
internal static class AnalyzerThroughputSource
{
    /// <summary>
    /// Builds a compilation unit with <paramref name="types"/> classes, each mixing
    /// valid and jagged parameter/argument lists so the analyzers hit both clean and
    /// violating paths.
    /// </summary>
    /// <param name="types">Number of classes to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types)
        => $$"""
           using System;
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => $$"""
           public class C{{i}}
           {
               public void Wrapped{{i}}(
                   string a,
                   int b,
                   bool c,
                   long d)
               {
                   Sink(a, b, c, d);
                   Sink(a,
                       b, c, d);
               }

               public void OneLine{{i}}(string a, int b, bool c, long d) { }
               private void Sink(string a, int b, bool c, long d) { }
           }
           """)}}
           """;
}
