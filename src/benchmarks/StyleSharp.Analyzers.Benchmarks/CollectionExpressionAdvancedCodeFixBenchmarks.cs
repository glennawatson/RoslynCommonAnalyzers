// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for advanced collection-expression code fixes.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CollectionExpressionAdvancedCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The representative expression target for expression-shaped fixes.</summary>
    private ExpressionSyntax _expression = null!;

    /// <summary>The representative local target for builder-sequence fixes.</summary>
    private LocalDeclarationStatementSyntax _local = null!;

    /// <summary>The diagnostic id for expression-shaped fixes.</summary>
    private string _diagnosticId = string.Empty;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Gets or sets the collection-expression shape under test.</summary>
    [Params(
        CollectionExpressionAdvancedBenchmarkShape.Stackalloc,
        CollectionExpressionAdvancedBenchmarkShape.Create,
        CollectionExpressionAdvancedBenchmarkShape.Builder,
        CollectionExpressionAdvancedBenchmarkShape.Fluent)]
    public CollectionExpressionAdvancedBenchmarkShape CurrentShape { get; set; }

    /// <summary>Builds the benchmark document and selects one representative target.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            CollectionExpressionAdvancedBenchmarkSource.GenerateCodeFix(Nodes, CurrentShape));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        if (CurrentShape == CollectionExpressionAdvancedBenchmarkShape.Builder)
        {
            _local = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<LocalDeclarationStatementSyntax>(
                _root,
                Nodes / MiddleNodeDivisor,
                static node => node.Declaration.Variables[0].Identifier.ValueText == "builder");
            return;
        }

        _expression = FindExpressionTarget();
        _diagnosticId = DiagnosticId(CurrentShape);
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

    /// <summary>Benchmarks applying one advanced collection-expression code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> CollectionExpressionAdvanced_ApplyFixAsync()
    {
        var updated = CurrentShape == CollectionExpressionAdvancedBenchmarkShape.Builder
            ? CollectionExpressionBuilderCodeFixProvider.Apply(_document, _root, _local)
            : await CollectionExpressionCodeFixProvider.ReplaceAsync(_document, _root, _expression, _diagnosticId).ConfigureAwait(false);
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

    /// <summary>Gets the diagnostic id for the selected expression-shaped fix.</summary>
    /// <param name="shape">The benchmark shape.</param>
    /// <returns>The diagnostic id.</returns>
    private static string DiagnosticId(CollectionExpressionAdvancedBenchmarkShape shape)
        => shape switch
        {
            CollectionExpressionAdvancedBenchmarkShape.Stackalloc => CollectionExpressionRules.UseCollectionExpressionForStackalloc.Id,
            CollectionExpressionAdvancedBenchmarkShape.Create => CollectionExpressionRules.UseCollectionExpressionForCreate.Id,
            _ => CollectionExpressionRules.UseCollectionExpressionForFluent.Id
        };

    /// <summary>Finds the representative expression for the selected shape.</summary>
    /// <returns>The expression target.</returns>
    private ExpressionSyntax FindExpressionTarget()
        => CurrentShape switch
        {
            CollectionExpressionAdvancedBenchmarkShape.Stackalloc
                => CodeFixBenchmarkSyntaxLookup.GetNthDescendant<StackAllocArrayCreationExpressionSyntax>(_root, Nodes / MiddleNodeDivisor, static _ => true),
            CollectionExpressionAdvancedBenchmarkShape.Create
                => CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
                    _root,
                    Nodes / MiddleNodeDivisor,
                    static node => node.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Create" }),
            _ => CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
                _root,
                Nodes / MiddleNodeDivisor,
                static node => node.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToList" })
        };
}
