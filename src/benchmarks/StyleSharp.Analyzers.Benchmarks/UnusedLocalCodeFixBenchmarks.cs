// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the unused-local code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class UnusedLocalCodeFixBenchmarks : IDisposable
{
    /// <summary>The representative node index used for the code-fix benchmark.</summary>
    private const int RepresentativeNodeIndex = 0;

    /// <summary>The name of the write-only local the benchmark fixes, which also has a dead write to rewrite.</summary>
    private const string RepresentativeLocalName = "state";

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The cached semantic model for the benchmark document.</summary>
    private SemanticModel _model = null!;

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
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            UnusedLocalBenchmarkSource.Generate(Nodes, violating: true));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _model = (await _document.GetSemanticModelAsync().ConfigureAwait(false))!;
        var declarator = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<VariableDeclaratorSyntax>(
            _root,
            RepresentativeNodeIndex,
            static candidate => candidate.Identifier.ValueText == RepresentativeLocalName);
        _diagnostic = Diagnostic.Create(
            MaintainabilityRules.UnusedLocal,
            declarator.Identifier.GetLocation(),
            declarator.Identifier.ValueText);
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

    /// <summary>Benchmarks applying one unused-local code fix, including the dead write it has to rewrite.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> UnusedLocal_ApplyFixAsync()
    {
        var updated = Sst1497UnusedLocalCodeFixProvider.Apply(_document, _root, _model, _diagnostic, CancellationToken.None);
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
