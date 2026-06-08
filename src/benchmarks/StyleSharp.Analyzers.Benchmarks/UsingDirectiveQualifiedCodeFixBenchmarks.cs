// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the using-directive-qualified code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class UsingDirectiveQualifiedCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative using directive.</summary>
    private StructuralCodeFixBenchmarkContext<UsingDirectiveSyntax> _context = null!;

    /// <summary>Stores the resolved symbol used when rewriting the representative using directive.</summary>
    private ISymbol _symbol = null!;

    /// <summary>Gets or sets the synthetic namespace count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative relative using directive.</summary>
    /// <returns>A task that completes when the benchmark context and symbol have been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            StructuralCodeFixBenchmarkSource.GenerateUsingDirectiveQualified,
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthDescendant<UsingDirectiveSyntax>(root, index, static _ => true)).ConfigureAwait(false);
        var model = await _context.Document.GetSemanticModelAsync().ConfigureAwait(false);
        _symbol = model!.GetSymbolInfo(_context.Node.Name!).Symbol!;
    }

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the using-directive-qualified fix to one representative using directive.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> UsingDirectiveQualified_ApplyFixAsync()
    {
        var updated = UsingDirectiveQualifiedCodeFixProvider.Replace(_context.Document, _context.Root, _context.Node.Name!, _symbol);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
