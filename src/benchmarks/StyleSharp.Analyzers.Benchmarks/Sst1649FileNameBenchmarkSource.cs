// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1649 file-name analyzer benchmarks.</summary>
internal static class Sst1649FileNameBenchmarkSource
{
    /// <summary>
    /// Builds a compilation unit that exercises the clean or violating file-name path.
    /// The benchmark compilation always uses the file path <c>Bench.cs</c>, so a single
    /// file yields at most one violation; the clean variant names the first type
    /// <c>Bench</c> and the violating variant names it something else. The requested
    /// type count is emitted as filler members so the file scales in size.
    /// </summary>
    /// <param name="types">The number of synthetic filler members to emit.</param>
    /// <param name="violating">Whether to emit a file-name rule violation.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => violating ? GenerateViolating(types) : GenerateClean(types);

    /// <summary>Builds the clean variant whose first type name matches the file stem <c>Bench</c>.</summary>
    /// <param name="types">The number of synthetic filler members to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateClean(int types)
        => $$"""
           namespace Bench;

           public sealed class Bench
           {
           {{BenchmarkSourceText.JoinLines(types, GenerateMember)}}
           }
           """;

    /// <summary>Builds the violating variant whose first type name does not match the file stem <c>Bench</c>.</summary>
    /// <param name="types">The number of synthetic filler members to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateViolating(int types)
        => $$"""
           namespace Bench;

           public sealed class Mismatch
           {
           {{BenchmarkSourceText.JoinLines(types, GenerateMember)}}
           }
           """;

    /// <summary>Builds one filler member so the synthetic file scales in size.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member text.</returns>
    private static string GenerateMember(int index)
        => $"    public int Value{index} => {index};";
}
