// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.CodeFixes;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the seal-attribute-types code-fix path.</summary>
[System.Diagnostics.DebuggerDisplay("SealAttributeTypesCodeFixBenchmarks: {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
public class SealAttributeTypesCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private CompilationUnitSyntax _root = null!;

    /// <summary>The diagnostic reported on the representative node passed to the code fix.</summary>
    private Diagnostic _diagnostic = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative attribute class.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, SealAttributeTypesBenchmarkSource.Generate(Nodes, violating: true));
        _root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var declaration = CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(_root, Nodes / MiddleNodeDivisor);
        _diagnostic = Diagnostic.Create(ApiSelectionRules.SealAttributeTypes, declaration.GetLocation());
    }

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Disposes the benchmark workspace.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Benchmarks applying the seal-attribute-types code fix to one representative class.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> SealAttributeTypes_ApplyFixAsync()
    {
        var updated = TargetCodeFix.Apply(_document, _root, ReportedNode.Ancestor<ClassDeclarationSyntax>(_root, _diagnostic)!, SealedModifierRewrite.AddSealed);
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
