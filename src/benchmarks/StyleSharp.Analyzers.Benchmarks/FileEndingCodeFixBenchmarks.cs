// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the file-ending code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FileEndingCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative end-of-file insertion span.</summary>
    private DirectCodeFixBenchmarkContext<TextSpan> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects the end-of-file insertion span.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateFileEnding,
            static async (document, _, _) =>
            {
                var text = await document.GetTextAsync().ConfigureAwait(false);
                return new TextSpan(text.Length, 0);
            }).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks normalizing one representative file ending to a single newline.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> FileEnding_ApplyFixAsync()
    {
        var updated = await FileEndingCodeFixProvider.NormaliseAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
