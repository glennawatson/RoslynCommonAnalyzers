// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for redundant-default-argument analyzer benchmarks.</summary>
internal static class RedundantDefaultArgumentBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating call sites.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit redundant-default-argument violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose call sites all pass something other than the default.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers each rejection route the no-diagnostic path takes: a call with no optional parameter at all, a
    /// call that omits the optional argument, a value that differs from the default, and a redundant value
    /// that is not the trailing argument.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public int Plain(int value) => value;

               public int Configure(int size = 8, bool cache = true) => size;

               public int Use()
               {
                   Plain(1);
                   Configure();
                   Configure(16);
                   Configure(8, false);
                   return Configure(cache: false);
               }
           }
           """;

    /// <summary>Builds one type whose trailing arguments all repeat their parameter's default.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Emits exactly five reported arguments: two from the run, then one each from the other three calls.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public int Configure(int size = 8, bool cache = true) => size;

               public int Retry(int attempts = 3) => attempts;

               public int Use()
               {
                   Configure(8, true);
                   Configure(cache: true);
                   Retry(3);
                   return Retry(attempts: 3);
               }
           }
           """;
}
