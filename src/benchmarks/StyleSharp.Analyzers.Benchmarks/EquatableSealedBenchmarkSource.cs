// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for equatable-should-be-sealed analyzer benchmarks.</summary>
internal static class EquatableSealedBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating equality contracts.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit equality-contract rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating equatable type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type per exit the no-diagnostic path takes.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// A type with no interfaces at all (the common case, and the one the loop over an empty interface list
    /// answers), a sealed equatable class, a struct, and a class that decides equality against some other
    /// type — which reaches the name comparison and the type-argument check before it is let go.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public class Plain{{index}}
           {
               public int Value { get; set; }
           }

           public sealed class Money{{index}} : IEquatable<Money{{index}}>
           {
               public int Cents { get; set; }

               public bool Equals(Money{{index}} other) => other is not null && other.Cents == Cents;
           }

           public struct Point{{index}} : IEquatable<Point{{index}}>
           {
               public int X { get; set; }

               public bool Equals(Point{{index}} other) => other.X == X;
           }

           public class Weight{{index}} : IEquatable<Point{{index}}>
           {
               public int Grams { get; set; }

               public bool Equals(Point{{index}} other) => other.X == Grams;
           }
           """;

    /// <summary>Builds one type that reaches the report.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>One diagnostic per type: an open class that claims to decide equality against itself.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public class Money{{index}} : IEquatable<Money{{index}}>
           {
               public int Cents { get; set; }

               public bool Equals(Money{{index}} other) => other is not null && other.Cents == Cents;
           }
           """;
}
