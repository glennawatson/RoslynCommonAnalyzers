// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for inheritdoc-validity analyzer (SST1648) benchmarks.</summary>
internal static class Sst1648InheritDocBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises valid or invalid inheritdoc usage.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit inheritdoc with no base to inherit from.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating inheritdoc-bearing type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one clean type whose inheritdoc member implements an interface member.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           internal interface I{{index}}
           {
               void M();
           }

           internal class C{{index}} : I{{index}}
           {
               /// <inheritdoc/>
               public void M()
               {
               }
           }
           """;

    /// <summary>Builds one violating type whose inheritdoc member has no base to inherit from.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           internal class C{{index}}
           {
               /// <inheritdoc/>
               public void M()
               {
               }
           }
           """;
}
