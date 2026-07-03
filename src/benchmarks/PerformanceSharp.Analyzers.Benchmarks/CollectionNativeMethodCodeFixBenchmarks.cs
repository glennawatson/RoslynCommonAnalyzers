// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the collection native-method code-fix paths (PSH1110, PSH1111).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CollectionNativeMethodCodeFixBenchmarks : IDisposable
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

    /// <summary>Gets or sets the collection native-method shape under test.</summary>
    [Params(
        CollectionNativeMethodBenchmarkShape.ListPredicate,
        CollectionNativeMethodBenchmarkShape.ArrayPredicate,
        CollectionNativeMethodBenchmarkShape.Membership)]
    public CollectionNativeMethodBenchmarkShape CurrentShape { get; set; }

    /// <summary>Builds the benchmark document and representative diagnostic.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            CollectionNativeMethodBenchmarkSource.GenerateCodeFix(Nodes, CurrentShape));
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

    /// <summary>Benchmarks applying one collection native-method code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> CollectionNativeMethod_ApplyFixAsync()
    {
        var updated = CollectionNativeMethodCodeFixProvider.Apply(_document, _root, _diagnostic);
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
            CollectionNativeMethodBenchmarkShape.ListPredicate => CreateNativePredicateDiagnostic("FirstOrDefault", "Find"),
            CollectionNativeMethodBenchmarkShape.ArrayPredicate => CreateNativePredicateDiagnostic("Any", "Array.Exists"),
            _ => CreateMembershipDiagnostic()
        };

    /// <summary>Creates a PSH1110 diagnostic carrying the replacement target name.</summary>
    /// <param name="invokedName">The invoked LINQ method name to locate.</param>
    /// <param name="target">The replacement target name.</param>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateNativePredicateDiagnostic(string invokedName, string target)
    {
        var name = FindInvokedName(invokedName);
        var properties = ImmutableDictionary<string, string?>.Empty.Add(CollectionNativeMethodAnalyzer.TargetNameKey, target);
        return Diagnostic.Create(CollectionRules.UseCollectionNativePredicate, name.GetLocation(), properties, target);
    }

    /// <summary>Creates a PSH1111 membership diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateMembershipDiagnostic()
        => Diagnostic.Create(CollectionRules.UseContainsForMembership, FindInvokedName("Any").GetLocation());

    /// <summary>Locates the middle invocation's method name within the benchmark corpus.</summary>
    /// <param name="invokedName">The invoked LINQ method name to locate.</param>
    /// <returns>The method name node.</returns>
    private SimpleNameSyntax FindInvokedName(string invokedName)
    {
        var invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            node => node.Expression is MemberAccessExpressionSyntax memberAccess && memberAccess.Name.Identifier.ValueText == invokedName);
        return ((MemberAccessExpressionSyntax)invocation.Expression).Name;
    }
}
