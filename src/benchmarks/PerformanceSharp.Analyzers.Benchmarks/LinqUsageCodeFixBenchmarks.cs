// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the LINQ collapse code-fix paths (PSH1101, PSH1102).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class LinqUsageCodeFixBenchmarks : IDisposable
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

    /// <summary>Gets or sets the LINQ usage shape under test.</summary>
    [Params(LinqUsageBenchmarkShape.WhereTerminal, LinqUsageBenchmarkShape.TypeFilter)]
    public LinqUsageBenchmarkShape CurrentShape { get; set; }

    /// <summary>Builds the benchmark document and representative diagnostic.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            LinqUsageBenchmarkSource.GenerateCodeFix(Nodes, CurrentShape));
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

    /// <summary>Benchmarks applying one LINQ collapse code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> LinqUsage_ApplyFixAsync()
    {
        var updated = LinqUsageCodeFixProvider.Apply(_document, _root, _diagnostic);
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
        => CurrentShape == LinqUsageBenchmarkShape.WhereTerminal
            ? CreateWhereTerminalDiagnostic()
            : CreateTypeFilterDiagnostic();

    /// <summary>Creates a LINQ Where-terminal diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateWhereTerminalDiagnostic()
    {
        var invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Any" });
        return Diagnostic.Create(CollectionRules.CollapseLinqWhereTerminal, ((MemberAccessExpressionSyntax)invocation.Expression).Name.GetLocation());
    }

    /// <summary>Creates a LINQ type-filter diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateTypeFilterDiagnostic()
    {
        var invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Cast" } });
        return Diagnostic.Create(CollectionRules.CollapseLinqTypeFilter, ((MemberAccessExpressionSyntax)invocation.Expression).Name.GetLocation());
    }
}
