// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the logical-operator folding analyzer benchmarks.</summary>
internal static class UseLogicalOperatorBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises the clean or the violating shape.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose conditionals already use logical operators.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public bool Check(bool ready, bool other) => ready && other;

               public bool Pick(bool ready, int count) => ready ? count : 0;
           }
           """;

    /// <summary>Builds one type whose conditionals carry a boolean literal branch.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public bool Check(bool ready, bool other) => ready ? other : false;

               public bool Either(bool ready, bool other) => ready ? true : other;
           }
           """;
}
