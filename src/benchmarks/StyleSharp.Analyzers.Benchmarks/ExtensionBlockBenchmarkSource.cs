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
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateContainer(i, violating))}}
           """;

    /// <summary>Builds one clean or violating extension-block container.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <param name="violating">Whether to emit a violating container.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateContainer(int index, bool violating)
        => violating ? GenerateViolatingContainer(index) : GenerateCleanContainer(index);

    /// <summary>Builds one clean extension-block container.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateCleanContainer(int index)
        => $$"""
           public static class Sample{{index}}Extensions
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
           """;

    /// <summary>Builds one violating extension-block container.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateViolatingContainer(int index)
        => $$"""
           public static class Sample{{index}}
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
           """;
}
