// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for property value-documentation analyzer (SST1609) benchmarks.</summary>
internal static class PropertyValueDocumentationBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating property value documentation.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit property value-documentation violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating property-bearing type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type whose documented property has a value element with text.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           internal class C{{index}}
           {
               /// <summary>Gets the count.</summary>
               /// <value>The count.</value>
               public int Count => 0;
           }
           """;

    /// <summary>Builds one violating type whose documented property omits the value element.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           internal class C{{index}}
           {
               /// <summary>Gets the count.</summary>
               public int Count => 0;
           }
           """;
}
