// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for raw-string-literal analysis.</summary>
internal static class RawStringLiteralBenchmarkSource
{
    /// <summary>The number of string literal shapes cycled by this source generator.</summary>
    private const int LiteralShapeCount = 2;

    /// <summary>Builds clean or violating verbatim string members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable literals.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class RawStringLiteralBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % LiteralShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public string Escaped{{index}}() => @"say ""hi"" number {{index}}";
                           """,
            (1, true) => $$"""
                           public string Lines{{index}}() => @"first {{index}}
                           second";
                           """,
            (0, false) => $$"""
                            public string Plain{{index}}() => @"plain text {{index}}";
                            """,
            _ => $$"""
                   public string Regular{{index}}() => "regular \n text {{index}}";
                   """
        };
}
