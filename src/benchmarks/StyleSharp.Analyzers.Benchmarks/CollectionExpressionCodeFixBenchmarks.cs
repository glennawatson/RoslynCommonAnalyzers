// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the collection-expression code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CollectionExpressionCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private CompilationUnitSyntax _root = null!;

    /// <summary>The representative collection expression target passed to the code fix.</summary>
    private ExpressionSyntax _expression = null!;

    /// <summary>The collection-expression diagnostic id applied to the representative target.</summary>
    private string _diagnosticId = string.Empty;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>The collection-expression benchmark shapes.</summary>
    public enum Shape
    {
        /// <summary>Uses an empty collection factory or literal.</summary>
        Empty,

        /// <summary>Uses an explicit collection initializer.</summary>
        Explicit
    }

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Gets or sets the collection-expression shape under test.</summary>
    [Params(Shape.Empty, Shape.Explicit)]
    public Shape CurrentShape { get; set; }

    /// <summary>Builds the benchmark document and selects one representative collection-expression target.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            ModernizationCodeFixBenchmarkSource.GenerateCollectionExpression(Nodes, CurrentShape == Shape.Explicit));
        _root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var method = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<MethodDeclarationSyntax>(_root, Nodes / MiddleNodeDivisor);
        _expression = (ExpressionSyntax)method.ExpressionBody!.Expression;
        _diagnosticId = CurrentShape == Shape.Explicit
            ? CollectionExpressionRules.UseExplicitCollectionExpression.Id
            : CollectionExpressionRules.UseEmptyCollectionExpression.Id;
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

    /// <summary>Benchmarks applying the collection-expression code fix to one representative node.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> CollectionExpression_ApplyFixAsync()
    {
        var updated = await CollectionExpressionCodeFixProvider.ReplaceAsync(_document, _root, _expression, _diagnosticId).ConfigureAwait(false);
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
