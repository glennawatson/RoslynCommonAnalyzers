// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for base-object-equality-delegation analyzer benchmarks.</summary>
internal static class BaseObjectEqualityDelegationBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating base-object-equality-delegation patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit base-object-equality-delegation rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating base-object-equality-delegation type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose equality members never delegate to object.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public override bool Equals(object obj) => obj is C{{index}};
           
               public override int GetHashCode() => {{index}};
           }
           """;

    /// <summary>Builds one type whose equality members delegate to object.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class C{{index}}
           {
               public override bool Equals(object obj) => base.Equals(obj);
           
               public override int GetHashCode() => base.GetHashCode();
           }
           """;
}
