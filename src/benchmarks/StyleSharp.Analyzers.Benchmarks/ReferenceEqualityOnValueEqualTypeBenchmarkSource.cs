// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for reference-equality analyzer benchmarks.</summary>
internal static class ReferenceEqualityOnValueEqualTypeBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating equality comparisons.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit reference-equality violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           public class Money
           {
               public decimal Amount { get; set; }

               public override bool Equals(object obj) => obj is Money other && other.Amount == Amount;

               public override int GetHashCode() => Amount.GetHashCode();
           }

           public class Weight
           {
               public int Grams { get; set; }

               public static bool operator ==(Weight left, Weight right) => Equals(left, right);

               public static bool operator !=(Weight left, Weight right) => !Equals(left, right);

               public override bool Equals(object obj) => obj is Weight other && other.Grams == Grams;

               public override int GetHashCode() => Grams;
           }

           public class Session
           {
               public int Id { get; set; }
           }

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose comparisons are all rejected.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route: the literal operand that stops the clean path before the bind, a value
    /// type, a framework type with its own operator, a user type with its own operator, an operand typed as
    /// object, and a type that never overrode Equals.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public bool Missing(Money value) => value == null;

               public bool Numbers(int left, int right) => left == right;

               public bool Text(string left, string right) => left == right;

               public bool Operators(Weight left, Weight right) => left == right;

               public bool Objects(Money left, object right) => left == right;

               public bool References(Session left, Session right) => left == right;
           }
           """;

    /// <summary>Builds one type whose comparisons all compare references on a value-equal type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public bool Same(Money left, Money right) => left == right;

               public bool Differ(Money left, Money right) => left != right;

               public bool Guarded(Money left, Money right, bool flag) => flag && left == right;
           }
           """;
}
