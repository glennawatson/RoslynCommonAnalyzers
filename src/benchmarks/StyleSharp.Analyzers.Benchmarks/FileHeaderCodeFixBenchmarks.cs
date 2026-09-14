// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the file-header code-fix path.</summary>
[System.Diagnostics.DebuggerDisplay("FileHeaderCodeFixBenchmarks: {Types}")]
[MemoryDiagnoser]
[ShortRunJob]
public class FileHeaderCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and the file-header diagnostic carrying the rendered header.</summary>
    private DirectCodeFixBenchmarkContext<Diagnostic> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and prepares the rendered file header text.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync() =>
        _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Types,
            DocumentationCodeFixBenchmarkSource.GenerateFileHeader,
            static (_, _, _) => Task.FromResult(CreateDiagnostic())).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks inserting one representative file header.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> FileHeader_ApplyFixAsync()
    {
        var updated = await TextChangeCodeFix.ApplyAsync(_context.Document, _context.Target, Sst1633FileHeaderCodeFixProvider.RegisterTextChanges, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Creates the file-header diagnostic carrying the rendered header text.</summary>
    /// <returns>The diagnostic the code fix resolves the header from.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic CreateDiagnostic() =>
        Diagnostic.Create(
            DocumentationRules.FileHeader,
            Location.None,
            ImmutableDictionary<string, string?>.Empty.Add(FileHeaderHelper.HeaderProperty, FileHeaderHelper.Render("Copyright text.", "Bench.cs")));
}
