// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the record-init-only code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class RecordInitOnlyCodeFixBenchmarks
{
    /// <summary>Identifies the representative property member within each generated record.</summary>
    private const int RepresentativePropertyMemberIndex = 0;

    /// <summary>Identifies the representative set accessor within the generated accessor list.</summary>
    private const int RepresentativeSetAccessorIndex = 1;

    /// <summary>Stores the prepared benchmark document and representative accessor declaration.</summary>
    private StructuralCodeFixBenchmarkContext<AccessorDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative record set accessor.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            StructuralCodeFixBenchmarkSource.GenerateRecordInitOnly,
            static (root, index)
                => ((PropertyDeclarationSyntax)CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<RecordDeclarationSyntax>(root, index).Members[RepresentativePropertyMemberIndex])
                    .AccessorList!
                    .Accessors[RepresentativeSetAccessorIndex]).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the record-init-only fix to one representative accessor.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> RecordInitOnly_ApplyFixAsync()
    {
        var updated = await RecordInitOnlyCodeFixProvider.ConvertAsync(_context.Document, _context.Node, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
