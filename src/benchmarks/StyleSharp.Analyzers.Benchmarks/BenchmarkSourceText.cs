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
    /// <remarks>
    /// Uses the literal "\n" rather than <see cref="Environment.NewLine"/> so the
    /// generated corpus has identical line counts on every OS, keeping parse size
    /// and line/span results deterministic across platforms.
    /// </remarks>
    public static string JoinBlocks(int count, Func<int, string> createBlock)
        => Join(count, "\n\n", createBlock);

    /// <summary>Joins generated lines with a deterministic newline sequence.</summary>
    /// <param name="count">The number of lines to generate.</param>
    /// <param name="createLine">Builds one line.</param>
    /// <returns>The joined line text.</returns>
    /// <remarks>
    /// Uses the literal "\n" rather than <see cref="Environment.NewLine"/> so the
    /// generated corpus has identical line counts on every OS, keeping parse size
    /// and line/span results deterministic across platforms.
    /// </remarks>
    public static string JoinLines(int count, Func<int, string> createLine)
        => Join(count, "\n", createLine);

    /// <summary>Builds joined text by repeatedly appending generated segments.</summary>
    /// <param name="count">The number of segments to generate.</param>
    /// <param name="separator">The separator inserted between generated segments.</param>
    /// <param name="createSegment">Builds one segment.</param>
    /// <returns>The joined text.</returns>
    private static string Join(int count, string separator, Func<int, string> createSegment)
    {
        if (count <= 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(separator);
            }

            builder.Append(createSegment(i));
        }

        return builder.ToString();
    }
}
