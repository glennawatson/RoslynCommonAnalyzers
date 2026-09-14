// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the documentation-header-spacing code-fix path.</summary>
[System.Diagnostics.DebuggerDisplay("DocumentationHeaderSpacingCodeFixBenchmarks: {Nodes}")]
[MemoryDiagnoser]
[ShortRunJob]
public class DocumentationHeaderSpacingCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and the diagnostic reported on the representative documented member.</summary>
    private DirectCodeFixBenchmarkContext<Diagnostic> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative documented member.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync() =>
        _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateDocumentationHeaderSpacing,
            static (_, root, index) => Task.FromResult(FindTarget(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks removing the blank line after one representative documentation header.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> DocumentationHeaderSpacing_ApplyFixAsync()
    {
        var updated = await TextChangeCodeFix.ApplyAsync(
            _context.Document,
            _context.Target,
            DocumentationHeaderSpacingCodeFixProvider.RegisterTextChanges,
            CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Creates the missing-blank-line diagnostic for the representative documented member.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based member index to select.</param>
    /// <returns>The diagnostic reported on the selected member.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic FindTarget(CompilationUnitSyntax root, int index) =>
        Diagnostic.Create(LayoutRules.DocHeaderNotFollowedByBlankLine, CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<MethodDeclarationSyntax>(root, index).GetLocation());
}
