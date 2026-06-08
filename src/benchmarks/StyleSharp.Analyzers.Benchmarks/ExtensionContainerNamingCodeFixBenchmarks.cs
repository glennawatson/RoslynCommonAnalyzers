// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the SST1704 code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ExtensionContainerNamingCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark type.</summary>
    private const int MiddleTypeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The identifier span of the representative extension container.</summary>
    private TextSpan _identifierSpan;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document once per parameter set.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, ExtensionContainerNamingCodeFixBenchmarkSource.Generate(Types));

        var root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var type = (ClassDeclarationSyntax)root.Members[Types / MiddleTypeDivisor];
        _identifierSpan = type.Identifier.Span;
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

    /// <summary>Benchmarks applying the SST1704 rename fix to one representative extension container.</summary>
    /// <returns>The updated solution project count.</returns>
    [Benchmark]
    public async Task<int> ExtensionContainerNaming_ApplyFixAsync()
    {
        var root = await _document.GetSyntaxRootAsync().ConfigureAwait(false);
        var declaration = (ClassDeclarationSyntax)root!.FindNode(_identifierSpan, getInnermostNodeForTie: true);
        var updated = await ExtensionContainerNamingCodeFixProvider.RenameAsync(
            _document,
            declaration,
            ExtensionContainerNaming.BuildPreferredName(declaration.Identifier.ValueText, ExtensionContainerNaming.ExtensionsSuffix),
            CancellationToken.None).ConfigureAwait(false);
        return updated.ProjectIds.Count;
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
