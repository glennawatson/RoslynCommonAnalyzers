// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the exception-handling analyzer benchmarks.</summary>
internal static class ExceptionHandlingBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating catch clauses.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit empty base-exception catches.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class ExceptionHandlingBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit an empty base-exception catch.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating
            ? $"internal static void M{index}() {{ try {{ }} catch (System.Exception) {{ }} }}"
            : $"internal static void M{index}() {{ try {{ }} catch (System.InvalidOperationException ex) {{ System.Console.WriteLine(ex); }} }}";
}
