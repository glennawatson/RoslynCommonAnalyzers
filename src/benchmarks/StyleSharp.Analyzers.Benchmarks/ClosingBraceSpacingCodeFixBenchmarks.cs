// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the closing-brace-spacing code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ClosingBraceSpacingCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative closing-brace token.</summary>
    private DirectCodeFixBenchmarkContext<SyntaxToken> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative closing brace token.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateClosingBraceSpacing,
            static (_, root, index) => Task.FromResult(FindCloseBrace(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks inserting the blank line after one representative closing brace.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> ClosingBraceSpacing_ApplyFixAsync()
    {
        var updated = await Sst1513ClosingBraceSpacingCodeFixProvider.InsertBlankLineAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative nested closing brace token in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based nested block index to select.</param>
    /// <returns>The selected closing brace token.</returns>
    private static SyntaxToken FindCloseBrace(CompilationUnitSyntax root, int index)
        => ((BlockSyntax)CodeFixBenchmarkSyntaxLookup.GetNthDescendant<IfStatementSyntax>(root, index, static _ => true).Statement).CloseBraceToken;
}
