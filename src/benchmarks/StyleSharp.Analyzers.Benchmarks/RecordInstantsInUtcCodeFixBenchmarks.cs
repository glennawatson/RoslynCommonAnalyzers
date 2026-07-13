// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the UTC-instant code fix.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class RecordInstantsInUtcCodeFixBenchmarks : IDisposable
{
    /// <summary>The representative node index used for the code-fix benchmark.</summary>
    private const int RepresentativeNodeIndex = 0;

    /// <summary>The local-clock property name the corpus reads.</summary>
    private const string NowName = "Now";

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The representative clock read passed to the code fix.</summary>
    private MemberAccessExpressionSyntax _access = null!;

    /// <summary>The UTC replacement built for the representative clock read.</summary>
    private MemberAccessExpressionSyntax _replacement = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document, the representative clock read, and its replacement.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        var source = RecordInstantsInUtcBenchmarkSource.Generate(Nodes, violating: true);
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, source);
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _access = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<MemberAccessExpressionSyntax>(
            _root,
            RepresentativeNodeIndex,
            static access => access.Name.Identifier.ValueText == NowName);

        var model = (await _document.GetSemanticModelAsync().ConfigureAwait(false))!;
        var diagnostic = Diagnostic.Create(ModernizationRules.RecordInstantsInUtc, _access.GetLocation(), "DateTime.Now");
        Sst2011RecordInstantsInUtcCodeFixProvider.TryBuildReplacement(_root, model, diagnostic, out _, out var replacement);
        _replacement = replacement!;
    }

    /// <summary>Disposes the benchmark workspace.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Disposes the benchmark workspace.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Benchmarks applying one UTC-instant code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> RecordInstantsInUtc_ApplyFixAsync()
    {
        var updated = Sst2011RecordInstantsInUtcCodeFixProvider.Apply(_document, _root, _access, _replacement);
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
