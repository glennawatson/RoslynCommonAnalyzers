// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for extension-block analyzer benchmarks.</summary>
internal static class ExtensionBlockBenchmarkSource
{
    /// <summary>Builds a compilation unit containing many extension-block containers.</summary>
    /// <param name="members">The number of extension containers to emit.</param>
    /// <param name="violating">Whether to emit extension-block rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => $$"""
           {{BenchmarkSourceText.JoinBlocks(members, i => violating
               ? $$"""
                 public static class Sample{{i}}
                 {
                     extension(string text)
                     {
                         public bool IsEmpty => text.Length == 0;
                     }

                     public static bool IsBlank(this string other) => other.Length == 0;

                     extension(string other)
                     {
                     }
                 }
                 """
               : $$"""
                 public static class Sample{{i}}Extensions
                 {
                     extension(int value)
                     {
                         public bool IsZero => value == 0;
                     }

                     extension(string text)
                     {
                         public bool IsEmpty => text.Length == 0;
                     }
                 }
                 """)}}
           """;
}
