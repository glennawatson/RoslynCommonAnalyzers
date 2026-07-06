// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for rethrow-only catch code fixes.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class RethrowOnlyCatchCodeFixBenchmarks : IDisposable
{
    /// <summary>The representative node index used for the code-fix benchmark.</summary>
    private const int RepresentativeNodeIndex = 0;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The representative catch clause passed to the code fix.</summary>
    private CatchClauseSyntax _catchClause = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and representative catch clause.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, RethrowOnlyCatchBenchmarkSource.Generate(Nodes, violating: true));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _catchClause = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<CatchClauseSyntax>(
            _root,
            RepresentativeNodeIndex,
            static clause => Sst1470RemoveRethrowOnlyCatchAnalyzer.IsRethrowOnly(clause));
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

    /// <summary>Benchmarks applying one rethrow-only catch code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> RethrowOnlyCatch_ApplyFixAsync()
    {
        var updated = Sst1470RemoveRethrowOnlyCatchCodeFixProvider.Apply(_document, _root, _catchClause);
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
