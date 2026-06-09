// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for duplicate-documentation analyzer (SST1625) benchmarks.</summary>
internal static class Sst1625DuplicateDocumentationBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or duplicate documentation text.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit duplicate-documentation violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating documented type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type whose documentation elements have distinct text.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           internal class C{{index}}
           {
               /// <summary>Does the work.</summary>
               /// <param name="x">The input value.</param>
               public void M(int x)
               {
               }
           }
           """;

    /// <summary>Builds one violating type whose param text repeats the summary text.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           internal class C{{index}}
           {
               /// <summary>The same text.</summary>
               /// <param name="x">The same text.</param>
               public void M(int x)
               {
               }
           }
           """;
}
