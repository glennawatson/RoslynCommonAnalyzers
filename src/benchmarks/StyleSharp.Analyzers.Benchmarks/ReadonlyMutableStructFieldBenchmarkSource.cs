// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for readonly-mutable-struct-field analysis.</summary>
internal static class ReadonlyMutableStructFieldBenchmarkSource
{
    /// <summary>Builds clean or violating readonly field declarations.</summary>
    /// <param name="members">The number of synthetic fields to emit.</param>
    /// <param name="violating">Whether to emit readonly mutable-struct fields.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal struct Mutable
           {
               public int Value;
           }

           internal readonly struct Immutable
           {
               private readonly int _value;
           }

           internal sealed class ReadonlyMutableStructFieldBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateField(i, violating))}}
           }
           """;

    /// <summary>Builds one synthetic field.</summary>
    /// <param name="index">The synthetic field index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated field source.</returns>
    private static string GenerateField(int index, bool violating)
        => violating
            ? $"private readonly Mutable _value{index};"
            : $"private readonly Immutable _value{index};";
}
