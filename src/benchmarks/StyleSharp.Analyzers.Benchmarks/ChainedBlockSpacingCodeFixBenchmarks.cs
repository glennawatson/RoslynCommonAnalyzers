// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the chained-block-spacing code-fix path.</summary>
[System.Diagnostics.DebuggerDisplay("ChainedBlockSpacingCodeFixBenchmarks: {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
public class ChainedBlockSpacingCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and the diagnostic reported on the representative chained keyword.</summary>
    private DirectCodeFixBenchmarkContext<Diagnostic> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative chained-block keyword.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync() =>
        _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateChainedBlockSpacing,
            static (_, root, index) => Task.FromResult(FindTarget(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks removing the blank line before one representative chained keyword.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> ChainedBlockSpacing_ApplyFixAsync()
    {
        var updated = await TextChangeCodeFix.ApplyAsync(_context.Document, _context.Target, ChainedBlockSpacingCodeFixProvider.RegisterTextChanges, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative chained-keyword span in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based chained block index to select.</param>
    /// <returns>The selected chained-keyword span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TextSpan FindElseSpan(CompilationUnitSyntax root, int index) =>
        CodeFixBenchmarkSyntaxLookup.GetNthDescendant<IfStatementSyntax>(root, index, static statement => statement.Else is not null).Else!.ElseKeyword.Span;

    /// <summary>Creates the missing-blank-line diagnostic for the representative chained keyword.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based chained block index to select.</param>
    /// <returns>The diagnostic reported on the selected chained keyword.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic FindTarget(CompilationUnitSyntax root, int index) =>
        Diagnostic.Create(LayoutRules.ChainedBlockNotPrecededByBlankLine, Location.Create(root.SyntaxTree, FindElseSpan(root, index)));
}
