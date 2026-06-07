// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Small helpers for composing synthetic benchmark source text.</summary>
internal static class BenchmarkSourceText
{
    /// <summary>Joins generated blocks with one blank line between them.</summary>
    /// <param name="count">The number of blocks to generate.</param>
    /// <param name="createBlock">Builds one block.</param>
    /// <returns>The joined block text.</returns>
    public static string JoinBlocks(int count, Func<int, string> createBlock)
        => string.Join(Environment.NewLine + Environment.NewLine, Enumerable.Range(0, count).Select(createBlock));

    /// <summary>Joins generated lines with the platform newline sequence.</summary>
    /// <param name="count">The number of lines to generate.</param>
    /// <param name="createLine">Builds one line.</param>
    /// <returns>The joined line text.</returns>
    public static string JoinLines(int count, Func<int, string> createLine)
        => string.Join(Environment.NewLine, Enumerable.Range(0, count).Select(createLine));
}
