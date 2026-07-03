// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for string-builder-inner-allocation analyzer benchmarks.</summary>
internal static class StringBuilderInnerAllocationBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating string builder formatting patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit string-builder-inner-allocation rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating string builder formatting type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean string builder formatting type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string M(int value)
               {
                   var builder = new System.Text.StringBuilder();
                   builder.AppendFormat("{0}", value);
                   return builder.ToString();
               }
           }
           """;

    /// <summary>Builds one violating string builder formatting type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public string M(int value)
               {
                   var builder = new System.Text.StringBuilder();
                   builder.Append(string.Format("{0}", value));
                   return builder.ToString();
               }
           }
           """;
}
