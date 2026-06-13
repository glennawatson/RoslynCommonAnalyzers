// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the SST1649 rename-file code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FileNameRenameCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document.</summary>
    private StructuralCodeFixBenchmarkContext<ClassDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic nested-type count controlling the document size.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds a single-type document of the requested size.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            static types => FileTypeNamespaceBenchmarkSource.Generate(types, violating: false),
            static (root, _) => CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(root, 0)).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks renaming the document to match its first type.</summary>
    /// <returns>The project's document count after the rename.</returns>
    [Benchmark]
    public async Task<int> FileNameRename_ApplyFixAsync()
    {
        var updated = await Sst1649FileNameCodeFixProvider.RenameAsync(_context.Document, "Root.cs", CancellationToken.None).ConfigureAwait(false);
        return updated.GetProject(_context.Document.Project.Id)!.DocumentIds.Count;
    }
}
