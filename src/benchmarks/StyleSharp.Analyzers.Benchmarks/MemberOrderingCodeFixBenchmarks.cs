// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the member-ordering code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MemberOrderingCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative field declaration.</summary>
    private StructuralCodeFixBenchmarkContext<FieldDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative out-of-order constant field.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            static count => MemberOrderingBenchmarkSource.Generate(count, violating: true),
            static (root, index) => (FieldDeclarationSyntax)CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(root, index).Members[1]).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the member-ordering fix to one representative member.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> MemberOrdering_ApplyFixAsync()
    {
        var updated = await MemberOrderingCodeFixProvider.MoveAsync(_context.Document, _context.Node, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
