// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for call-async-in-async-context analyzer benchmarks.</summary>
internal static class CallAsyncInAsyncContextBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating blocking-call patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit call-async-in-async-context rule violations.</param>
    /// <returns>The generated source text.</returns>
    internal static string Generate(int types, bool violating) =>
        $$"""
           using System.Threading.Tasks;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating blocking-call type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating) =>
        violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type that calls the async sibling and awaits it.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index) =>
        $$"""
           public sealed class C{{index}}
           {
               public async Task<int> M() => await LoadAsync(1);

               public static int Load(int value) => value;

               public static Task<int> LoadAsync(int value) => Task.FromResult(value);
           }
           """;

    /// <summary>Builds one violating type that calls the synchronous method inside an async one, with a sibling that fits.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index) =>
        $$"""
           public sealed class C{{index}}
           {
               public async Task<int> M() => Load(1);

               public static int Load(int value) => value;

               public static Task<int> LoadAsync(int value) => Task.FromResult(value);
           }
           """;
}
