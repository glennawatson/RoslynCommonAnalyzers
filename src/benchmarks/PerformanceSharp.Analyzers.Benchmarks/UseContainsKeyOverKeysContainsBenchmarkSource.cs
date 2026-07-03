// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for use-ContainsKey-over-Keys.Contains analyzer benchmarks.</summary>
internal static class UseContainsKeyOverKeysContainsBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating dictionary key-membership patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit use-ContainsKey-over-Keys.Contains rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating key-membership type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean key-membership type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly System.Collections.Generic.IDictionary<string, int> _map = new System.Collections.Generic.Dictionary<string, int>();

               public bool M(string key) => _map.ContainsKey(key);
           }
           """;

    /// <summary>Builds one violating key-membership type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private readonly System.Collections.Generic.IDictionary<string, int> _map = new System.Collections.Generic.Dictionary<string, int>();

               public bool M(string key) => _map.Keys.Contains(key);
           }
           """;
}
