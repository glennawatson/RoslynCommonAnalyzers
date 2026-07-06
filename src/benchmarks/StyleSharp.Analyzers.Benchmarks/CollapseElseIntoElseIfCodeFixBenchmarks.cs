// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for collapse-else-into-else-if code fixes.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CollapseElseIntoElseIfCodeFixBenchmarks : IDisposable
{
    /// <summary>The representative node index used for the code-fix benchmark.</summary>
    private const int RepresentativeNodeIndex = 0;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The representative diagnostic passed to the code fix.</summary>
    private Diagnostic _diagnostic = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and representative diagnostic.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, CollapseElseIntoElseIfBenchmarkSource.Generate(Nodes, violating: true));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var elseClause = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<ElseClauseSyntax>(_root, RepresentativeNodeIndex, static _ => true);
        _diagnostic = Diagnostic.Create(MaintainabilityRules.CollapseElseIntoElseIf, elseClause.ElseKeyword.GetLocation());
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

    /// <summary>Benchmarks applying one collapse-else code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> CollapseElseIntoElseIf_ApplyFixAsync()
    {
        var updated = Sst1465CollapseElseIntoElseIfCodeFixProvider.Apply(_document, _root, _diagnostic);
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
