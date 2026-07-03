// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the LINQ chain code-fix paths (PSH1107, PSH1108, PSH1109).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class LinqChainCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

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

    /// <summary>Gets or sets the LINQ chain shape under test.</summary>
    [Params(LinqChainBenchmarkShape.FilterBeforeSort, LinqChainBenchmarkShape.UseThenBy, LinqChainBenchmarkShape.MergeConsecutiveWhere)]
    public LinqChainBenchmarkShape CurrentShape { get; set; }

    /// <summary>Builds the benchmark document and representative diagnostic.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            LinqChainBenchmarkSource.GenerateCodeFix(Nodes, CurrentShape));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _diagnostic = CreateDiagnostic();
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

    /// <summary>Benchmarks applying one LINQ chain code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> LinqChain_ApplyFixAsync()
    {
        var updated = LinqChainCodeFixProvider.Apply(_document, _root, _diagnostic);
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

    /// <summary>Creates the representative diagnostic for the selected shape.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateDiagnostic()
        => CurrentShape switch
        {
            LinqChainBenchmarkShape.FilterBeforeSort => CreateFilterBeforeSortDiagnostic(),
            LinqChainBenchmarkShape.UseThenBy => CreateUseThenByDiagnostic(),
            _ => CreateMergeConsecutiveWhereDiagnostic()
        };

    /// <summary>Creates a filter-after-sort diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateFilterBeforeSortDiagnostic()
    {
        var invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Where", Expression: InvocationExpressionSyntax });
        return Diagnostic.Create(CollectionRules.FilterBeforeSort, ((MemberAccessExpressionSyntax)invocation.Expression).Name.GetLocation());
    }

    /// <summary>Creates a repeated-sort diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateUseThenByDiagnostic()
    {
        var invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "OrderBy", Expression: InvocationExpressionSyntax });
        return Diagnostic.Create(CollectionRules.UseThenBy, ((MemberAccessExpressionSyntax)invocation.Expression).Name.GetLocation(), "ThenBy");
    }

    /// <summary>Creates a consecutive-Where diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateMergeConsecutiveWhereDiagnostic()
    {
        var invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Where", Expression: InvocationExpressionSyntax });
        return Diagnostic.Create(CollectionRules.MergeConsecutiveWhere, ((MemberAccessExpressionSyntax)invocation.Expression).Name.GetLocation());
    }
}
