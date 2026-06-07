// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for member-documentation analyzer benchmarks.</summary>
internal static class MemberDocumentationBenchmarkSource
{
    /// <summary>Builds a compilation unit containing many documented methods.</summary>
    /// <param name="members">The number of methods to emit.</param>
    /// <param name="violating">Whether to omit required parameter and return docs.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;
           internal sealed class DocumentationBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating documented member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit incomplete documentation.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => violating ? GenerateViolatingMember(index) : GenerateCleanMember(index);

    /// <summary>Builds one clean documented member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateCleanMember(int index)
        => $$"""
           /// <summary>Does the work.</summary>
           /// <param name="value">The input value.</param>
           /// <returns>The input value.</returns>
           internal int M{{index}}(int value) => value;
           """;

    /// <summary>Builds one violating documented member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateViolatingMember(int index)
        => $$"""
           /// <summary>Does the work.</summary>
           internal int M{{index}}(int value) => value;
           """;
}
