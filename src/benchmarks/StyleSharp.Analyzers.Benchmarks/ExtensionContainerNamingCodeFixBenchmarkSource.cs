// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the SST1704 code-fix benchmarks.</summary>
internal static class ExtensionContainerNamingCodeFixBenchmarkSource
{
    /// <summary>Builds a compilation unit containing many SST1704 violations.</summary>
    /// <param name="types">The number of synthetic extension containers to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types)
        => $$"""
           {{BenchmarkSourceText.JoinBlocks(types, GenerateType)}}
           """;

    /// <summary>Builds one violating extension container.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index)
        => $$"""
           public static class Sample{{index}}Container
           {
               extension(string text)
               {
                   public bool IsEmpty => text.Length == 0;
               }
           }
           """;
}
