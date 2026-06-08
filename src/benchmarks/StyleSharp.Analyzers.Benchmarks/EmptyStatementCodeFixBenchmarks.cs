// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the empty-statement code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class EmptyStatementCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative empty statement.</summary>
    private StructuralCodeFixBenchmarkContext<EmptyStatementSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative stray semicolon.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            StructuralCodeFixBenchmarkSource.GenerateEmptyStatement,
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthDescendant<EmptyStatementSyntax>(root, index, static _ => true)).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the empty-statement fix to one representative semicolon.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> EmptyStatement_ApplyFixAsync()
    {
        var updated = await Sst1106EmptyStatementCodeFixProvider.RemoveAsync(_context.Document, _context.Root, _context.Node).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
