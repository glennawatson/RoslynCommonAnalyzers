// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for value-type null comparison analysis.</summary>
internal static class ValueTypeNullComparisonBenchmarkSource
{
    /// <summary>The number of comparison shapes cycled by this source generator.</summary>
    private const int ComparisonShapeCount = 4;

    /// <summary>The cycled index of the third comparison shape.</summary>
    private const int ThirdShapeIndex = 2;

    /// <summary>The cycled index of the fourth comparison shape.</summary>
    private const int FourthShapeIndex = 3;

    /// <summary>Builds clean or violating null-comparison members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable comparisons.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal enum Mode
           {
               First,
               Second
           }

           internal struct Money
           {
               public static bool operator ==(Money left, Money right) => true;

               public static bool operator !=(Money left, Money right) => false;
           }

           internal sealed class ValueTypeNullComparisonBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % ComparisonShapeCount, violating) switch
        {
            (0, true) => $"    public bool Int{index}(int value) => value == null;",
            (1, true) => $"    public bool Date{index}(System.DateTime value) => value != null;",
            (ThirdShapeIndex, true) => $"    public bool Mode{index}(Mode value) => value == null;",
            (FourthShapeIndex, true) => $"    public bool Money{index}(Money value) => value != null;",
            (0, false) => $"    public bool Int{index}(int? value) => value == null;",
            (1, false) => $"    public bool Text{index}(string value) => value != null;",
            (ThirdShapeIndex, false) => $"    public bool Reference{index}(object value) => value == null;",
            _ => $"    public bool Money{index}(Money? value) => value != null;"
        };
}
