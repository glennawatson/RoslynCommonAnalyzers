// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for identical-operand analyzer benchmarks.</summary>
internal static class IdenticalOperandsBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating operand pairs.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit identical-operand rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating operand type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose operators all have operands that differ, produce side effects, or belong to SST1473.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every route the no-diagnostic path takes: operands of different kinds (rejected on the kind
    /// compare), operands of the same kind but different content (rejected by the structural walk), operands
    /// that are structurally identical but call something or index something (rejected by the purity test),
    /// and the floating-point NaN idiom (the only route that reaches the semantic model).
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private int _seed;

               private readonly int[] _values = new int[4];

               public int Next() => _seed++;

               public bool Compare(int left, int right) => left == right;

               public bool Advance() => Next() == Next();

               public int Difference(int left, int right) => left - right;

               public bool Both(bool left, bool right) => left && right;

               public int Half(int value) => value / 2;

               public int Mask(int value, int mask) => value & mask;

               public bool NanIdiom(double value) => value != value;

               public bool Indexed(int index) => _values[index] == _values[index];

               public int Twice(int value) => value + value;
           }
           """;

    /// <summary>Builds one type whose operators all compare an expression with itself.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Fourteen operators, fourteen diagnostics: every reported operator once, plus a field and a qualified member chain.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               private int _value;

               public bool Same(int value) => value == value;

               public bool NotSame(int value) => value != value;

               public bool Less(int value) => value < value;

               public bool AtLeast(int value) => value >= value;

               public bool Both(bool flag) => flag && flag;

               public bool Either(bool flag) => flag || flag;

               public int Mask(int value) => value & value;

               public int Merge(int value) => value | value;

               public int Toggle(int value) => value ^ value;

               public int Zero(int value) => value - value;

               public int Unit(int value) => value / value;

               public int Remainder(int value) => value % value;

               public bool Field() => _value == _value;

               public bool Qualified() => this._value >= this._value;
           }
           """;
}
