// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the nested-if merge code fix.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MergeNestedIfCodeFixBenchmarks : IDisposable
{
    /// <summary>The representative node index used for the code-fix benchmark.</summary>
    private const int RepresentativeNodeIndex = 0;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The representative outer if statement passed to the code fix.</summary>
    private IfStatementSyntax _outer = null!;

    /// <summary>The inner if statement the outer one wraps.</summary>
    private IfStatementSyntax _inner = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and the representative nested-if pair.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        var source = MergeNestedIfBenchmarkSource.Generate(Nodes, violating: true);
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, source);
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _outer = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<IfStatementSyntax>(
            _root,
            RepresentativeNodeIndex,
            static statement => Sst2013MergeNestedIfAnalyzer.GetMergeableInnerIf(statement) is not null);
        _inner = Sst2013MergeNestedIfAnalyzer.GetMergeableInnerIf(_outer)!;
    }

    /// <summary>Disposes the benchmark workspace.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Disposes the benchmark workspace.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Benchmarks applying one nested-if merge.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> MergeNestedIf_ApplyFixAsync()
    {
        var updated = Sst2013MergeNestedIfCodeFixProvider.Apply(_document, _root, _outer, _inner);
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
