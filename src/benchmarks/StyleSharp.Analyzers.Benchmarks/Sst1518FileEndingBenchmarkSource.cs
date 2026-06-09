// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1518 file-ending analyzer benchmarks.</summary>
internal static class Sst1518FileEndingBenchmarkSource
{
    /// <summary>Builds a whole-file compilation unit with a correct or incorrect file ending.</summary>
    /// <param name="members">The number of synthetic members used to scale the file size.</param>
    /// <param name="violating">Whether to omit the single trailing newline.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
    {
        var body = "namespace Bench;\n\n"
            + "internal sealed class FileEndingBench\n{\n"
            + BenchmarkSourceText.JoinBlocks(members, GenerateMethod)
            + "\n}";

        // Clean ends with exactly one newline after the final closing brace; violating omits it.
        return violating ? body : body + "\n";
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
