// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for readonly-generic-field-write analyzer benchmarks (SST2421).</summary>
internal static class ReadonlyGenericFieldWriteBenchmarkSource
{
    /// <summary>Builds a compilation unit that writes through readonly generic fields.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    /// <remarks>
    /// A single shared <c>IPoint</c> interface backs every generated holder; the clean holder constrains its
    /// type parameter to a reference type, so the write lands and the rule stays silent.
    /// </remarks>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           public interface IPoint
           {
               int X { get; set; }
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one reference-constrained holder whose write lands on the referenced instance.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}<T>
               where T : class, IPoint
           {
               private readonly T _p;

               public C{{index}}(T p) => _p = p;

               public void M() => _p.X = 5;
           }
           """;

    /// <summary>Builds one unconstrained holder whose write lands on a defensive struct copy.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}<T>
               where T : IPoint
           {
               private readonly T _p;

               public V{{index}}(T p) => _p = p;

               public void M() => _p.X = 5;
           }
           """;
}
