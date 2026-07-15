// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the wrong-logger-category benchmark (SST2443).</summary>
internal static class LoggerCategoryBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating logger categories.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           {{LoggerBenchmarkShim.Shim}}

           namespace Bench
           {
               using Microsoft.Extensions.Logging;

               public sealed class Payload { public void Work() { } }

               {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           }
           """;

    /// <summary>Builds one type categorized by itself.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private ILogger<C{{index}}> _logger;
           }
           """;

    /// <summary>Builds one type categorized by another type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private ILogger<Payload> _logger;
           }
           """;
}
