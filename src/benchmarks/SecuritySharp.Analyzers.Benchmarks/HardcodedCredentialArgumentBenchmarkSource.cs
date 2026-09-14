// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers.Benchmarks;

/// <summary>Builds credential calls alongside ordinary logging and placeholder arguments.</summary>
internal static class HardcodedCredentialArgumentBenchmarkSource
{
    /// <summary>Creates repeated calls with a variable or hardcoded credential.</summary>
    /// <param name="nodes">The number of types to generate.</param>
    /// <param name="violating">Whether the credential argument is a reportable literal.</param>
    /// <returns>The generated compilation source.</returns>
    internal static string Generate(int nodes, bool violating) =>
        $$"""
        namespace Bench;

        {{BenchmarkSourceText.JoinBlocks(nodes, i => GenerateType(i, violating))}}
        """;

    /// <summary>Creates one type with candidate and clean argument shapes.</summary>
    /// <param name="index">The type index.</param>
    /// <param name="violating">Whether the credential is hardcoded.</param>
    /// <returns>The type declaration.</returns>
    private static string GenerateType(int index, bool violating) =>
        $$"""
        public static class C{{index}}
        {
            public static void Run(string credential)
            {
                Authenticate({{(violating ? "\"private-access-value\"" : "credential")}});
                Authenticate("");
                Log("ordinary message");
            }

            private static void Authenticate(string password) { }
            private static void Log(string message) { }
        }
        """;
}
