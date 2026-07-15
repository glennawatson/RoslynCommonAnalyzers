// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for per-call HttpClient analyzer benchmarks.</summary>
internal static class PerCallHttpClientBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating HttpClient patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Net.Http;
           using System.Threading.Tasks;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one clean type, which shares one client instance.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private static readonly HttpClient Client = new HttpClient();

               public Task<string> M(string url) => Client.GetStringAsync(url);

               public HttpClient Create() => new HttpClient();
           }
           """;

    /// <summary>Builds one violating type, which constructs a client per call.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public async Task<string> M(string url)
               {
                   using var client = new HttpClient();
                   return await client.GetStringAsync(url);
               }
           }
           """;
}
