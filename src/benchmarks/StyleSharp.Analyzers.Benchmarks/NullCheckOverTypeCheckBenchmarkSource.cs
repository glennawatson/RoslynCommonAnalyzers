// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the null-check-over-type-check benchmarks (SST2019).</summary>
internal static class NullCheckOverTypeCheckBenchmarkSource
{
    /// <summary>Builds a compilation unit whose type tests are already null checks, or are not.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose tests already say what they mean.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The clean corpus keeps real type tests in it, so the measured cost includes the work of deciding
    /// that a right-hand side is some type other than object.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public class Clean{{index}}
           {
               public bool Present{{index}}(string value) => value is not null;

               public bool Absent{{index}}(string value) => value is null;

               public bool Typed{{index}}(object value) => value is string;
           }
           """;

    /// <summary>Builds one type that tests against object in both directions.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class Violating{{index}}
           {
               public bool Present{{index}}(string value) => value is object;

               public bool Absent{{index}}(string value) => value is not object;

               public bool Typed{{index}}(object value) => value is string;
           }
           """;
}
