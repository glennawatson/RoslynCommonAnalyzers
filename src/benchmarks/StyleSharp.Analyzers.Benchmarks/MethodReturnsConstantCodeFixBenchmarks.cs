// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Memory benchmarks for the method-returns-constant code-fix path. The fix rewrites every call site, so it
/// resolves references across the solution — this measures that whole conversion, not just the declaration.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MethodReturnsConstantCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The representative constant-returning method passed to the code fix.</summary>
    private MethodDeclarationSyntax _method = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative method.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, MethodReturnsConstantBenchmarkSource.Generate(Nodes, violating: true));
        var root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _method = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<MethodDeclarationSyntax>(
            root,
            Nodes / MiddleNodeDivisor,
            static method => Sst1493MethodReturnsConstantAnalyzer.TryGetConstantBody(method) is not null);
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

    /// <summary>Benchmarks converting one representative method into a get-only property.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> MethodReturnsConstant_ApplyFixAsync()
    {
        var updated = await Sst1493MethodReturnsConstantCodeFixProvider
            .ConvertAsync(_document, _method, CancellationToken.None)
            .ConfigureAwait(false);
        return (await updated.GetDocument(_document.Id)!.GetTextAsync().ConfigureAwait(false)).Length;
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
