// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the SST1402 move-type-to-file code-fix path.</summary>
[System.Diagnostics.DebuggerDisplay("MoveTypeToFileCodeFixBenchmarks: {Types}")]
[MemoryDiagnoser]
[ShortRunJob]
public class MoveTypeToFileCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative type to move.</summary>
    private StructuralCodeFixBenchmarkContext<ClassDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic top-level type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document of many top-level types and selects a middle type to move.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync() =>
        _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            static types => FileTypeNamespaceBenchmarkSource.Generate(types, violating: true),
            CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks moving one representative type out to its own file.</summary>
    /// <returns>The updated original document's text length.</returns>
    /// <exception cref="InvalidOperationException">The applied fix dropped the original document from the solution, so its text cannot be measured.</exception>
    [Benchmark]
    public async Task<int> MoveTypeToFile_ApplyFixAsync()
    {
        var updated = await Sst1402MoveTypeToFileCodeFixProvider.MoveAsync(_context.Document, _context.Node, "Moved.cs", CancellationToken.None).ConfigureAwait(false);
        var document = updated.GetDocument(_context.Document.Id)
            ?? throw new InvalidOperationException("The fix dropped the document the benchmark measures.");
        return (await document.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
