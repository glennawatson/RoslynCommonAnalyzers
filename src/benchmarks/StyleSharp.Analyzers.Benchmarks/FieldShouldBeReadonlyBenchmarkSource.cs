// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for field-should-be-readonly analyzer benchmarks.</summary>
/// <remarks>
/// The fields are packed into a single large type rather than many one-field types, because the
/// analyzer's cost is per-field re-scanning of the whole containing type. A single type with N
/// candidate fields is the realistic worst case the rule pays and is what this benchmark measures.
/// </remarks>
internal static class FieldShouldBeReadonlyBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating readonly-field patterns.</summary>
    /// <param name="fields">The number of private fields to emit in the single synthetic type.</param>
    /// <param name="violating">Whether to emit readonly-field violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int fields, bool violating)
        => violating ? GenerateViolating(fields) : GenerateClean(fields);

    /// <summary>Builds one type whose fields are all assigned only in the constructor (every field is reported).</summary>
    /// <param name="fields">The number of private fields to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateViolating(int fields)
        => $$"""
           namespace Bench;

           internal sealed class Big
           {
           {{BenchmarkSourceText.JoinLines(fields, i => $"private int _f{i};")}}
           internal Big(int value)
           {
           {{BenchmarkSourceText.JoinLines(fields, i => $"_f{i} = value;")}}
           }
           }
           """;

    /// <summary>Builds one type whose fields are also written in a method, so none are reported.</summary>
    /// <param name="fields">The number of private fields to emit.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateClean(int fields)
        => $$"""
           namespace Bench;

           internal sealed class Big
           {
           {{BenchmarkSourceText.JoinLines(fields, i => $"private int _f{i};")}}
           internal Big(int value)
           {
           {{BenchmarkSourceText.JoinLines(fields, i => $"_f{i} = value;")}}
           }
           internal void Reset(int value)
           {
           {{BenchmarkSourceText.JoinLines(fields, i => $"_f{i} = value;")}}
           }
           }
           """;
}
