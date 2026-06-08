// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the file-start-blank-lines code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FileStartBlankLinesCodeFixBenchmarks
{
    /// <summary>The line index of the first non-blank line in the generated file-start benchmark source.</summary>
    private const int FirstNonBlankLineIndex = 2;

    /// <summary>The prepared benchmark document and representative leading-blank-lines span.</summary>
    private DirectCodeFixBenchmarkContext<TextSpan> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects the leading blank-line span.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateFileStartBlankLines,
            static async (document, _, _) =>
            {
                var text = await document.GetTextAsync().ConfigureAwait(false);
                return TextSpan.FromBounds(0, text.Lines[FirstNonBlankLineIndex].Start);
            }).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks removing the leading blank lines from one representative file.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> FileStartBlankLines_ApplyFixAsync()
    {
        var updated = await Sst1517FileStartBlankLinesCodeFixProvider.RemoveAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
