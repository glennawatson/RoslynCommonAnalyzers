// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the field-should-be-readonly code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FieldShouldBeReadonlyCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative field declaration.</summary>
    private StructuralCodeFixBenchmarkContext<FieldDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic field count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Fields { get; set; }

    /// <summary>Builds the benchmark document and selects one representative constructor-only field assignment.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    /// <remarks>
    /// The corpus is a single type holding every generated field, so the representative node is the
    /// middle field of that type. Selecting the middle <em>type</em> instead asked for a class that is
    /// never emitted, and setup threw before the benchmark could run.
    /// </remarks>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Fields,
            static count => FieldShouldBeReadonlyBenchmarkSource.Generate(count, violating: true),
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<FieldDeclarationSyntax>(root, index)).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the field-should-be-readonly fix to one representative field.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> FieldShouldBeReadonly_ApplyFixAsync()
    {
        var updated = Sst1424FieldShouldBeReadonlyCodeFixProvider.AddReadonly(_context.Document, _context.Root, _context.Node);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
