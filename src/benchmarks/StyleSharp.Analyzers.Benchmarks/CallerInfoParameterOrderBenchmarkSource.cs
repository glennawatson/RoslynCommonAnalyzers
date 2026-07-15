// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for caller-info parameter-order analyzer benchmarks (SST2433).</summary>
internal static class CallerInfoParameterOrderBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating caller-info parameters.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Runtime.CompilerServices;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           """;

    /// <summary>Builds one type whose caller-info parameter comes last with a default.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void Write(string message, [CallerMemberName] string caller = "")
               {
               }
           }
           """;

    /// <summary>Builds one type whose caller-info parameter is followed by an ordinary parameter.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void Write([CallerMemberName] string caller = "", string message = "")
               {
               }
           }
           """;
}
