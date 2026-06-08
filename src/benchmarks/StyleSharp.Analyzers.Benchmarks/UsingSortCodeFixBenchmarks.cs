// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the using-sort code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class UsingSortCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative compilation unit.</summary>
    private StructuralCodeFixBenchmarkContext<CompilationUnitSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic using-pair count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects the root container that owns the unsorted using directives.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            StructuralCodeFixBenchmarkSource.GenerateUsingSort,
            static (root, _) => root).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the using-sort fix to one representative using container.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> UsingSort_ApplyFixAsync()
    {
        var updated = await UsingSortCodeFixProvider.SortAsync(_context.Document, _context.Node, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
