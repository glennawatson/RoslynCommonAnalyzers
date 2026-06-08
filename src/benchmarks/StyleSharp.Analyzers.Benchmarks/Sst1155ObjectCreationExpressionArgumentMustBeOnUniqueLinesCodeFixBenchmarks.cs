// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the SST1155 code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class Sst1155ObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark state for the current parameterized run.</summary>
    private UniqueLinesCodeFixBenchmarkContext<ObjectCreationExpressionSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Members { get; set; }

    /// <summary>Builds the benchmark document and selects one representative unique-lines violation.</summary>
    /// <returns>A task that completes when the benchmark state has been prepared.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await UniqueLinesCodeFixBenchmarkHelper.CreateAsync<ObjectCreationExpressionSyntax>(
            Members,
            UniqueLinesCodeFixBenchmarkSource.GenerateObjectCreationArguments).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the unique-lines code fix to one representative violation.</summary>
    /// <returns>The updated document text length after the code fix is applied.</returns>
    [Benchmark]
    public async Task<int> Sst1155ObjectCreationExpressionArgumentMustBeOnUniqueLines_ApplyFixAsync()
    {
        var updated = await Sst1155ObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider.FixAsync(
            _context.Document,
            _context.Root,
            _context.Node).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
