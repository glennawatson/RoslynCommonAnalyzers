// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds source for whole-file comment analysis.</summary>
internal static class EntireFileCommentedOutBenchmarkSource
{
    /// <summary>Builds the selected source shape.</summary>
    /// <param name="nodes">The number of declarations or comment lines.</param>
    /// <param name="path">The source shape.</param>
    /// <returns>The generated source text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The source shape is unknown.</exception>
    internal static string Generate(int nodes, string path) => path switch
    {
        "startup" => "internal sealed class Startup { }",
        "clean" => GenerateTypes(nodes),
        "prose" => BenchmarkSourceText.JoinBlocks(nodes, static i => $"// This explains example {i}."),
        "line-comment" => BenchmarkSourceText.JoinBlocks(nodes, static i => $"// public class C{i} {{ public int Read() => {i}; }}"),
        "block-comment" => $"/* {GenerateTypes(nodes)} */",
        "incomplete" => BenchmarkSourceText.JoinBlocks(nodes, static i => $"// public policy statement {i}."),
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown source shape."),
    };

    /// <summary>Builds complete type declarations.</summary>
    /// <param name="nodes">The number of types.</param>
    /// <returns>The source declarations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateTypes(int nodes) =>
        BenchmarkSourceText.JoinBlocks(nodes, static i => $"public class C{i} {{ public int Read() => {i}; }}");
}
