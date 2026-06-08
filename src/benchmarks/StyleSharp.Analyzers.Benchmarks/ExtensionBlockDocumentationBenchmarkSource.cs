// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for extension-block documentation benchmarks.</summary>
internal static class ExtensionBlockDocumentationBenchmarkSource
{
    /// <summary>Builds many extension-block containers, documented or with documentation issues.</summary>
    /// <param name="members">The number of extension containers to emit.</param>
    /// <param name="violating">Whether to emit documentation violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int members, bool violating)
        => BenchmarkSourceText.JoinBlocks(members, index => violating ? GenerateViolatingContainer(index) : GenerateCleanContainer(index));

    /// <summary>Builds many documented containers whose receiver parameter lacks a <c>&lt;param&gt;</c>.</summary>
    /// <param name="members">The number of extension containers to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateUndocumentedParameter(int members)
        => BenchmarkSourceText.JoinBlocks(members, GenerateUndocumentedParameterContainer);

    /// <summary>Builds one fully documented extension-block container.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateCleanContainer(int index)
        => $$"""
           public static class Sample{{index}}Extensions
           {
               /// <summary>Adds helpers to a value {{index}}.</summary>
               /// <typeparam name="T">The element type.</typeparam>
               /// <param name="value">The value.</param>
               extension<T>(T value)
               {
                   public bool IsDefault{{index}} => value is null;
               }
           }
           """;

    /// <summary>Builds one container exercising the missing-summary, missing-coverage, and invalid-reference paths.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateViolatingContainer(int index)
        => $$"""
           public static class Sample{{index}}Extensions
           {
               /// <summary>Adds helpers to a value {{index}}.</summary>
               /// <param name="missing">Not a parameter.</param>
               extension<T>(T value)
               {
                   public bool IsDefault{{index}} => value is null;
               }
           }
           """;

    /// <summary>Builds one documented container whose receiver parameter has no <c>&lt;param&gt;</c>.</summary>
    /// <param name="index">The synthetic container index.</param>
    /// <returns>The generated container block.</returns>
    private static string GenerateUndocumentedParameterContainer(int index)
        => $$"""
           public static class Sample{{index}}Extensions
           {
               /// <summary>Adds helpers to a value {{index}}.</summary>
               /// <typeparam name="T">The element type.</typeparam>
               extension<T>(T value)
               {
                   public bool IsDefault{{index}} => value is null;
               }
           }
           """;
}
