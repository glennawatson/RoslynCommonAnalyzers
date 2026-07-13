// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the use-memory-based-stream-overloads code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class UseMemoryBasedStreamOverloadsCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private CompilationUnitSyntax _root = null!;

    /// <summary>The semantic model used to resolve the representative stream call.</summary>
    private SemanticModel _model = null!;

    /// <summary>The representative stream call passed to the code fix.</summary>
    private InvocationExpressionSyntax _invocation = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative stream call.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, UseMemoryBasedStreamOverloadsBenchmarkSource.Generate(Nodes, violating: true));
        _root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _model = (await _document.GetSemanticModelAsync().ConfigureAwait(false))!;
        var type = CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(_root, Nodes / MiddleNodeDivisor);
        _invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            type,
            0,
            static invocation => invocation.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "ReadAsync");
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

    /// <summary>Benchmarks applying the use-memory-based-stream-overloads code fix to one representative stream call.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> UseMemoryBasedStreamOverloads_ApplyFixAsync()
    {
        var updated = Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider.Apply(_document, _root, _model, _invocation);
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
