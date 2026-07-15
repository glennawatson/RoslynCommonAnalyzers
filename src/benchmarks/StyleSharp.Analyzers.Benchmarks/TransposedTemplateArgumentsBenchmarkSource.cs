// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the transposed-log-values benchmark (SST2440).</summary>
internal static class TransposedTemplateArgumentsBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating value order.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           {{LoggerBenchmarkShim.Shim}}

           namespace Bench
           {
               using Microsoft.Extensions.Logging;

               {{BenchmarkSourceText.JoinBlocks(types, i => violating ? Violating(i) : Clean(i))}}
           }
           """;

    /// <summary>Builds one type whose values follow the placeholders' order.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Clean(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public void M(ILogger logger, int first, int second)
                   => logger.LogInformation("{First} {Second}", first, second);
           }
           """;

    /// <summary>Builds one type whose two values are transposed.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string Violating(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public void M(ILogger logger, int first, int second)
                   => logger.LogInformation("{First} {Second}", second, first);
           }
           """;
}
