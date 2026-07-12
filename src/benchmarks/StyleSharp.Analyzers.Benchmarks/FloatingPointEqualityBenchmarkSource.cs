// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for floating-point-equality analyzer benchmarks.</summary>
internal static class FloatingPointEqualityBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating floating-point comparisons.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit floating-point-equality rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating floating-point type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type whose comparisons are all correct, producing no diagnostics.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every route the no-diagnostic path takes: a relational operator that never reaches the
    /// semantic model, a zero comparison rejected on the literal's text, an operand no floating-point value
    /// could be (a bool literal), and the three bound-but-clean types — decimal, int and string.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               private const double Tolerance = 1e-9;

               public bool Near(double left, double right) => System.Math.Abs(left - right) < Tolerance;

               public bool Ordered(double left, double right) => left > right;

               public bool Missing(double value) => double.IsNaN(value);

               public bool Unset(double value) => value == 0.0;

               public bool Settled(decimal price, decimal total) => price == total;

               public bool Counted(int left, int right) => left == right;

               public bool Named(string left, string right) => left == right;

               public bool Ready(bool flag) => flag != false;
           }
           """;

    /// <summary>Builds one type whose comparisons are all reported.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>Seven comparisons, seven diagnostics: exact double, exact float, both NaN equalities, a self-comparison, a relational NaN, and a lifted pair.</remarks>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public bool Same(double left, double right) => left == right;

               public bool Different(float left, float right) => left != right;

               public bool Missing(double value) => value == double.NaN;

               public bool Present(double value) => value != double.NaN;

               public bool Usable(double value) => value == value;

               public bool Below(double value) => value < double.NaN;

               public bool Lifted(double? left, double? right) => left == right;
           }
           """;
}
