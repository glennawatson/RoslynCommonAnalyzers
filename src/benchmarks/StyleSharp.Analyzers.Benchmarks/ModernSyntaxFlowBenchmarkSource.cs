// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for flow-shaped modern syntax analysis.</summary>
internal static class ModernSyntaxFlowBenchmarkSource
{
    /// <summary>The number of flow syntax shapes cycled by this source generator.</summary>
    private const int FlowShapeCount = 2;

    /// <summary>Builds clean or violating flow-syntax members.</summary>
    /// <param name="members">The number of synthetic methods to emit.</param>
    /// <param name="violating">Whether to emit reportable shapes.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           #nullable enable

           using System;

           namespace Bench;

           internal sealed class ModernSyntaxFlowBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a reportable shape.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMember(int index, bool violating)
        => (index % FlowShapeCount, violating) switch
        {
            (0, true) => $$"""
                           public string Guard{{index}}(string? value)
                           {
                               if (value is null)
                               {
                                   throw new ArgumentNullException(nameof(value));
                               }

                               return value;
                           }
                           """,
            (1, true) => $$"""
                           public int Parse{{index}}(string text)
                           {
                               int value;
                               if (int.TryParse(text, out value))
                               {
                                   return value;
                               }

                               return 0;
                           }
                           """,
            (0, false) => $$"""
                            public string Guard{{index}}(string? value) => value ?? throw new ArgumentNullException(nameof(value));
                            """,
            _ => $$"""
                   public int Parse{{index}}(string text)
                   {
                       if (int.TryParse(text, out var value))
                       {
                           return value;
                       }

                       return 0;
                   }
                   """
        };
}
