// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the no-public-on-internal-type code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class NoPublicOnInternalTypeCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative method declaration.</summary>
    private StructuralCodeFixBenchmarkContext<MethodDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative public member in an internal type.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            StructuralCodeFixBenchmarkSource.GenerateNoPublicOnInternalType,
            static (root, index) => (MethodDeclarationSyntax)CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(root, index).Members[0]).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the no-public-on-internal-type fix to one representative member.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> NoPublicOnInternalType_ApplyFixAsync()
    {
        var updated = await NoPublicOnInternalTypeCodeFixProvider.MakeInternalAsync(_context.Document, _context.Node, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
