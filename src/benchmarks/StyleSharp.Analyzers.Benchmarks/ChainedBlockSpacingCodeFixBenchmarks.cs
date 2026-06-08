// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the chained-block-spacing code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ChainedBlockSpacingCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative chained-keyword span.</summary>
    private DirectCodeFixBenchmarkContext<TextSpan> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative chained-block keyword.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateChainedBlockSpacing,
            static (_, root, index) => Task.FromResult(FindElseSpan(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks removing the blank line before one representative chained keyword.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> ChainedBlockSpacing_ApplyFixAsync()
    {
        var updated = await ChainedBlockSpacingCodeFixProvider.RemoveAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative chained-keyword span in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based chained block index to select.</param>
    /// <returns>The selected chained-keyword span.</returns>
    private static TextSpan FindElseSpan(CompilationUnitSyntax root, int index)
        => CodeFixBenchmarkSyntaxLookup.GetNthDescendant<IfStatementSyntax>(root, index, static statement => statement.Else is not null).Else!.ElseKeyword.Span;
}
