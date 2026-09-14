// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.CodeFixes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the numeric-literal-suffix code-fix path.</summary>
[System.Diagnostics.DebuggerDisplay("UppercaseLiteralSuffixCodeFixBenchmarks: {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
public class UppercaseLiteralSuffixCodeFixBenchmarks : IDisposable
{
    /// <summary>The representative node index used for the code-fix benchmark.</summary>
    private const int RepresentativeNodeIndex = 0;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The diagnostic reported on the representative node passed to the code fix.</summary>
    private Diagnostic _diagnostic = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative literal.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, UppercaseLiteralSuffixBenchmarkSource.Generate(Nodes, violating: true));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _diagnostic = Diagnostic.Create(ModernSyntaxRules.UppercaseLiteralSuffix, CodeFixBenchmarkSyntaxLookup.GetNthDescendant<LiteralExpressionSyntax>(
            _root,
            RepresentativeNodeIndex,
            static candidate => Sst2244UppercaseLiteralSuffixAnalyzer.TryGetLowercaseSuffix(candidate.Token.Text, out _)).GetLocation());
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

    /// <summary>Benchmarks applying the numeric-literal-suffix code fix to one representative literal.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> UppercaseLiteralSuffix_ApplyFixAsync()
    {
        var updated = ReplaceNodeCodeFix.Apply(_document, _root, _diagnostic, Sst2244UppercaseLiteralSuffixCodeFixProvider.TryRewrite);
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
