// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for operator-set analyzer benchmarks.</summary>
internal static class OperatorOverloadsBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating operator sets.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit operator-set rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating operator set.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose operator set is complete, plus one that declares no operators at all.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// The type with no operators is the common case and never reaches a check: the analyzer is driven from
    /// the operator declaration. The complete type pays for every lookup the rule can make — both equality
    /// overrides, both relational pairs, and the ordering contract — and is still not reported.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public class Plain{{index}}
           {
               public int Value { get; set; }
           }

           public sealed class Level{{index}} : IComparable<Level{{index}}>
           {
               public int Rank { get; set; }

               public int CompareTo(Level{{index}} other) => other is null ? 1 : Rank.CompareTo(other.Rank);

               public override bool Equals(object obj) => obj is Level{{index}} other && other.Rank == Rank;

               public override int GetHashCode() => Rank;

               public static bool operator ==(Level{{index}} left, Level{{index}} right) => Equals(left, right);

               public static bool operator !=(Level{{index}} left, Level{{index}} right) => !Equals(left, right);

               public static bool operator <(Level{{index}} left, Level{{index}} right) => left is null ? right is not null : left.CompareTo(right) < 0;

               public static bool operator >(Level{{index}} left, Level{{index}} right) => left is not null && left.CompareTo(right) > 0;

               public static bool operator <=(Level{{index}} left, Level{{index}} right) => left is null || left.CompareTo(right) <= 0;

               public static bool operator >=(Level{{index}} left, Level{{index}} right) => left is null ? right is null : left.CompareTo(right) >= 0;
           }
           """;

    /// <summary>Builds one type that reaches both reports.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Two diagnostics per type: the equality gap on <c>==</c>, and the two ordering gaps together on
    /// <c>&lt;</c>. The mirror operators are declared because the language will not compile the type
    /// without them, and they are silent — which is the point of the rule.
    /// </remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class Money{{index}}
           {
               public int Cents { get; set; }

               public static bool operator ==(Money{{index}} left, Money{{index}} right) => left?.Cents == right?.Cents;

               public static bool operator !=(Money{{index}} left, Money{{index}} right) => left?.Cents != right?.Cents;

               public static bool operator <(Money{{index}} left, Money{{index}} right) => left?.Cents < right?.Cents;

               public static bool operator >(Money{{index}} left, Money{{index}} right) => left?.Cents > right?.Cents;
           }
           """;
}
