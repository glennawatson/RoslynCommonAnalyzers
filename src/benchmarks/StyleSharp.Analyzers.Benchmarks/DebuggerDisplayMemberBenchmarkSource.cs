// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for debugger-display analyzer benchmarks (SST2405).</summary>
internal static class DebuggerDisplayMemberBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating display strings.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System.Diagnostics;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => violating ? GenerateViolatingType(i) : GenerateCleanType(i))}}
           """;

    /// <summary>Builds one type whose display string names members it declares.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route: a plain member, one with a format specifier, a called one, an expression
    /// too complex to check, and an ordinary attribute the name comparison must reject outright.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           [DebuggerDisplay("{Amount,nq} {Describe()} {Amount.ToString()}")]
           [System.Serializable]
           public sealed class C{{index}}
           {
               public decimal Amount { get; }

               public string Describe() => Amount.ToString();
           }
           """;

    /// <summary>Builds one type whose display string names a member it does not declare.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           [DebuggerDisplay("{Total,nq}")]
           public sealed class V{{index}}
           {
               public decimal Amount { get; }
           }
           """;
}
