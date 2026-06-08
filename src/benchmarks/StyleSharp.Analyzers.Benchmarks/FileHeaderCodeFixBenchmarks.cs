// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the file-header code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FileHeaderCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and rendered file-header text.</summary>
    private DirectCodeFixBenchmarkContext<string> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and prepares the rendered file header text.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Types,
            DocumentationCodeFixBenchmarkSource.GenerateFileHeader,
            static (_, _, _) => Task.FromResult(FileHeaderHelper.Render("Copyright text.", "Bench.cs"))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks inserting one representative file header.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> FileHeader_ApplyFixAsync()
    {
        var updated = await Sst1633FileHeaderCodeFixProvider.AddHeaderAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
