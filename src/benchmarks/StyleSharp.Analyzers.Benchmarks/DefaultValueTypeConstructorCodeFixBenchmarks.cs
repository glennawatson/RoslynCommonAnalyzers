// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.CodeFixes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the default-value-type-constructor code-fix path.</summary>
[System.Diagnostics.DebuggerDisplay("DefaultValueTypeConstructorCodeFixBenchmarks: {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
public class DefaultValueTypeConstructorCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private CompilationUnitSyntax _root = null!;

    /// <summary>The representative diagnostic on a value-type construction passed to the code fix.</summary>
    private Diagnostic _diagnostic = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative value-type construction.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, SemanticTypeBenchmarkSource.GenerateDefaultValueTypeConstructor(Nodes, violating: true));
        _root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var method = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<MethodDeclarationSyntax>(_root, Nodes / MiddleNodeDivisor);
        var creation = (ObjectCreationExpressionSyntax)method.ExpressionBody!.Expression;
        _diagnostic = Diagnostic.Create(ReadabilityRules.DefaultValueTypeConstructor, creation.GetLocation());
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

    /// <summary>Benchmarks applying the default-value-type-constructor code fix to one representative node.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> DefaultValueTypeConstructor_ApplyFixAsync()
    {
        var updated = TargetCodeFix.Apply(_document, _root, ReportedNode.Find<ObjectCreationExpressionSyntax>(_root, _diagnostic)!, Sst1129DefaultValueTypeConstructorCodeFixProvider.CreateDefault);
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
