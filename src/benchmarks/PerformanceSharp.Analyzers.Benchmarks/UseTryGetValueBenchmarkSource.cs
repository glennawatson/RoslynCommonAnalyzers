// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for use-TryGetValue analyzer benchmarks.</summary>
internal static class UseTryGetValueBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating ContainsKey guard patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit use-TryGetValue rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating dictionary-lookup type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean dictionary-lookup type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly System.Collections.Generic.Dictionary<string, int> _map = new();

               public int M(string key)
               {
                   if (_map.TryGetValue(key, out var value))
                   {
                       return value;
                   }

                   return 0;
               }
           }
           """;

    /// <summary>Builds one violating dictionary-lookup type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly System.Collections.Generic.Dictionary<string, int> _map = new();

               public int M(string key)
               {
                   if (_map.ContainsKey(key))
                   {
                       return _map[key];
                   }

                   return 0;
               }
           }
           """;
}
