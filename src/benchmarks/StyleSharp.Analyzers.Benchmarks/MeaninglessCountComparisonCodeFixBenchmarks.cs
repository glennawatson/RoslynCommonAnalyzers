// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the meaningless count-comparison code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MeaninglessCountComparisonCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The representative decided comparison passed to the code fix.</summary>
    private BinaryExpressionSyntax _comparison = null!;

    /// <summary>The constant the representative comparison folds to.</summary>
    private bool _result;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative comparison.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            MeaninglessCountComparisonBenchmarkSource.Generate(Nodes, violating: true));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _comparison = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<BinaryExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static binary => Sst1479MeaninglessCountComparisonAnalyzer.TryGetConstantResult(binary, out _));
        _result = Sst1479MeaninglessCountComparisonAnalyzer.TryGetConstantResult(_comparison, out var folded) && folded;
    }

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Disposes the benchmark workspace.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Benchmarks folding one representative comparison to its constant.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> MeaninglessCountComparison_ApplyFixAsync()
    {
        var updated = Sst1479MeaninglessCountComparisonCodeFixProvider.Apply(_document, _root, _comparison, _result);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Disposes managed state owned by the benchmark instance.</summary>
    /// <param name="disposing">Whether managed state should be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _workspace.Dispose();
        _disposed = true;
    }
}
