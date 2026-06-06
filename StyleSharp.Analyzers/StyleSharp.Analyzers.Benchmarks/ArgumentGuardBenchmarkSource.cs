// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for SST20xx argument-guard benchmarks.</summary>
internal static class ArgumentGuardBenchmarkSource
{
    /// <summary>Builds a compilation unit containing repeated guard clauses.</summary>
    /// <param name="nodes">The number of if statements to emit.</param>
    /// <param name="violating">Whether to emit guards that should be rewritten to throw helpers.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int nodes, bool violating)
    {
        var statement = violating
            ? """        if (value is null) throw new ArgumentNullException(nameof(value));"""
            : """        if (value.Length == 0) throw new InvalidOperationException();""";
        return $$"""
               using System;
               namespace Bench;
               public static class GuardBench
               {
                   public static void Check(string value)
                   {
               {{BenchmarkSourceText.JoinLines(nodes, _ => statement)}}
                   }
               }
               """;
    }
}
