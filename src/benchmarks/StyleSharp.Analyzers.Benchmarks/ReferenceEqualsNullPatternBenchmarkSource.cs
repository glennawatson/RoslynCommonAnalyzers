// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for reference-equality null-pattern analyzer benchmarks.</summary>
internal static class ReferenceEqualsNullPatternBenchmarkSource
{
    /// <summary>How often the clean corpus emits a near-miss member rather than unrelated code.</summary>
    private const int NearMissInterval = 4;

    /// <summary>Builds a compilation unit that exercises clean or violating null checks.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reference-equality null checks.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal static class ReferenceEqualsNullPatternBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reference-equality null check.</param>
    /// <returns>The generated member block.</returns>
    /// <remarks>
    /// The clean corpus is mostly code the rule turns back, because rejecting nodes is where an analyzer
    /// spends its time. One member in <see cref="NearMissInterval"/> is still a near-miss, so the walk
    /// that binds a two-argument static call stays covered.
    /// </remarks>
    private static string GenerateMember(int index, bool violating)
    {
        if (violating)
        {
            return GenerateViolatingMember(index);
        }

        return index % NearMissInterval == 0 ? GenerateCleanMember(index) : GenerateUnrelatedMember(index);
    }

    /// <summary>Builds one member with no reference-equality call at all.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateUnrelatedMember(int index)
        => $$"""
           internal static bool M{{index}}(string value, string other)
           {
               return string.Equals(value, other, System.StringComparison.Ordinal) || value.Length > other.Length;
           }
           """;

    /// <summary>Builds one member that already uses a null pattern.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    /// <remarks>
    /// The clean corpus keeps the same call shape as the violating one so the two measure the same walk:
    /// the analyzer still binds a two-argument static call per member, and only the null-operand test
    /// separates them.
    /// </remarks>
    private static string GenerateCleanMember(int index)
        => $$"""
           internal static bool M{{index}}(string value, string other)
           {
               return value is null || object.ReferenceEquals(value, other);
           }
           """;

    /// <summary>Builds one member whose null check goes through reference equality.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           internal static bool M{{index}}(string value, string other)
           {
               return object.ReferenceEquals(value, null) || object.ReferenceEquals(other, null);
           }
           """;
}
