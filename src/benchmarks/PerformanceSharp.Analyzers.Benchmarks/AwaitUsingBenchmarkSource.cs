// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for synchronous-using-in-async analyzer benchmarks.</summary>
internal static class AwaitUsingBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating using-disposal patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;
           using System.Threading.Tasks;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type: an awaited disposal in async code and a synchronous disposal in sync code.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class Resource{{index}} : IDisposable, IAsyncDisposable
           {
               public void Dispose()
               {
               }

               public ValueTask DisposeAsync() => default;
           }

           public sealed class C{{index}}
           {
               public async Task MAsync()
               {
                   await using var resource = new Resource{{index}}();
                   await Task.Yield();
               }

               public void M()
               {
                   using var resource = new Resource{{index}}();
               }
           }
           """;

    /// <summary>Builds one violating type: a synchronous using declaration inside an async method.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class Resource{{index}} : IDisposable, IAsyncDisposable
           {
               public void Dispose()
               {
               }

               public ValueTask DisposeAsync() => default;
           }

           public sealed class C{{index}}
           {
               public async Task MAsync()
               {
                   using var resource = new Resource{{index}}();
                   await Task.Yield();
               }
           }
           """;
}
