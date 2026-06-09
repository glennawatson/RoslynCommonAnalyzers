// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1517 file-start-blank-lines analyzer benchmarks.</summary>
internal static class Sst1517FileStartBlankLinesBenchmarkSource
{
    /// <summary>Builds a whole-file compilation unit with or without leading blank lines.</summary>
    /// <param name="members">The number of synthetic members used to scale the file size.</param>
    /// <param name="violating">Whether to prefix the file with blank lines.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
    {
        var lead = violating ? "\n\n" : string.Empty;
        return lead
            + "namespace Bench;\n\n"
            + "internal sealed class FileStartBlankLinesBench\n{\n"
            + BenchmarkSourceText.JoinBlocks(members, GenerateMethod)
            + "\n}\n";
    }

    /// <summary>Builds one simple method body used to scale the benchmark file size.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMethod(int index)
        => $$"""
           private static int M{{index}}()
           {
               return {{index}};
           }
           """;
}
