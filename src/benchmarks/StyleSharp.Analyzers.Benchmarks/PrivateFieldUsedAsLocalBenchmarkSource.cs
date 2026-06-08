// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for private-field-used-as-local analyzer benchmarks.</summary>
/// <remarks>
/// The fields are packed into a single large type rather than many one-field types, because the
/// analyzer's cost is per-field re-scanning of the whole containing type to confirm the field is
/// used by a single method. A single type with N candidate fields is the realistic worst case.
/// </remarks>
internal static class PrivateFieldUsedAsLocalBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating resettable-field patterns.</summary>
    /// <param name="fields">The number of private fields to emit in the single synthetic type.</param>
    /// <param name="violating">Whether to emit private-field-used-as-local violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int fields, bool violating)
        => violating ? GenerateViolating(fields) : GenerateClean(fields);

    /// <summary>Builds one type whose fields are each used as method-local scratch state (every field is reported).</summary>
    /// <param name="fields">The number of private fields to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateViolating(int fields)
        => $$"""
           namespace Bench;

           internal sealed class Big
           {
           {{BenchmarkSourceText.JoinBlocks(fields, GenerateScratchMember)}}
           }
           """;

    /// <summary>Builds one type whose fields carry initializers, so each is rejected before the single-method scan.</summary>
    /// <param name="fields">The number of private fields to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateClean(int fields)
        => $$"""
           namespace Bench;

           internal sealed class Big
           {
           {{BenchmarkSourceText.JoinBlocks(fields, GenerateInitializedMember)}}
           }
           """;

    /// <summary>Builds one field reset and used as scratch by its own method.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateScratchMember(int index)
        => $$"""
           private int _f{{index}};
           internal int M{{index}}(int value)
           {
           _f{{index}} = 0;
           _f{{index}} += value;
           return _f{{index}};
           }
           """;

    /// <summary>Builds one initialized field with a trivial reader, rejected before the expensive scan.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateInitializedMember(int index)
        => $$"""
           private int _f{{index}} = {{index}};
           internal int M{{index}}()
           {
           return _f{{index}};
           }
           """;
}
