// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the record-readonly code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class RecordReadonlyCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative record declaration.</summary>
    private StructuralCodeFixBenchmarkContext<RecordDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative non-readonly record struct.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            StructuralCodeFixBenchmarkSource.GenerateRecordReadonly,
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<RecordDeclarationSyntax>(root, index)).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the record-readonly fix to one representative record struct.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> RecordReadonly_ApplyFixAsync()
    {
        var updated = await RecordReadonlyCodeFixProvider.AddReadonlyAsync(_context.Document, _context.Node, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
