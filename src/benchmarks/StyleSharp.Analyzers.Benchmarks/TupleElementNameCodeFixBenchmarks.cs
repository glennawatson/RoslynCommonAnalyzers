// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the tuple-element-name code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class TupleElementNameCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private CompilationUnitSyntax _root = null!;

    /// <summary>The representative tuple element identifier passed to the code fix.</summary>
    private IdentifierNameSyntax _identifier = null!;

    /// <summary>The replacement tuple element name selected for the representative identifier.</summary>
    private string _name = string.Empty;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative tuple element access.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, TupleElementNameBenchmarkSource.Generate(Nodes, violating: true));
        _root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;

        var access = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<MemberAccessExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static candidate => candidate.Name.Identifier.ValueText == "Item1");
        _identifier = (IdentifierNameSyntax)access.Name;

        var model = await _document.GetSemanticModelAsync().ConfigureAwait(false);
        _ = Sst1142TupleElementNameAnalyzer.TryGetReplacementName(access, model!, CancellationToken.None, out var name);
        _name = name!;
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

    /// <summary>Benchmarks applying the tuple-element-name code fix to one representative node.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> TupleElementName_ApplyFixAsync()
    {
        var updated = Sst1142TupleElementNameCodeFixProvider.Replace(_document, _root, _identifier, _name);
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
