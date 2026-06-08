// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the accessor-consistency code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class AccessorConsistencyCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative property declaration.</summary>
    private StructuralCodeFixBenchmarkContext<PropertyDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative inconsistent accessor list.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            StructuralCodeFixBenchmarkSource.GenerateAccessorConsistency,
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<PropertyDeclarationSyntax>(root, index)).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the accessor-consistency fix to one representative property.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> AccessorConsistency_ApplyFixAsync()
    {
        var updated = await AccessorConsistencyCodeFixProvider.ExpandAsync(_context.Document, _context.Node.AccessorList!, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
