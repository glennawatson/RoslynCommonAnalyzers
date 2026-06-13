// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the method-naming analyzer benchmarks.</summary>
internal static class MethodNamingBenchmarkSource
{
    /// <summary>Builds a compilation unit of clean or violating task-returning methods.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit task methods missing the 'Async' suffix.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class MethodNamingBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a task method without the 'Async' suffix.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating
            ? $"internal static System.Threading.Tasks.Task Load{index}() => System.Threading.Tasks.Task.CompletedTask;"
            : $"internal static System.Threading.Tasks.Task Done{index}Async() => System.Threading.Tasks.Task.CompletedTask;";
}
