// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for use-AnyAsync-over-CountAsync analyzer benchmarks.</summary>
/// <remarks>
/// CountAsync and AnyAsync are provider extension methods rather than framework APIs, so the corpus
/// emits its own stand-in async query provider once in the header, ahead of the generated types.
/// </remarks>
internal static class UseAnyAsyncOverCountAsyncBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating awaited CountAsync() comparisons.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit use-AnyAsync-over-CountAsync rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Linq;
           using System.Threading;
           using System.Threading.Tasks;

           namespace Bench;

           public static class AsyncQuery
           {
               public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(0);

               public static Task<bool> AnyAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(false);
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating awaited CountAsync() comparison type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type, whose comparison genuinely needs the counted value.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async Task<bool> M(IQueryable<int> query) => await query.CountAsync() > 5;
           }
           """;

    /// <summary>Builds one violating type, whose comparison only asks whether the sequence has elements.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async Task<bool> M(IQueryable<int> query) => await query.CountAsync() > 0;
           }
           """;
}
