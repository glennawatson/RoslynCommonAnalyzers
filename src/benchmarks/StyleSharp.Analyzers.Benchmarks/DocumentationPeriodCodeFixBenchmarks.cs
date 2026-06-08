// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the documentation-period code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DocumentationPeriodCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative period-insertion position.</summary>
    private DirectCodeFixBenchmarkContext<int> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative period insertion position.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            DocumentationCodeFixBenchmarkSource.GenerateDocumentationPeriod,
            static (_, root, index) => Task.FromResult(FindInsertPosition(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks appending the period to one representative documentation element.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> DocumentationPeriod_ApplyFixAsync()
    {
        var updated = await DocumentationPeriodCodeFixProvider.AddPeriodAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative documentation-period insertion position in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based method index to select.</param>
    /// <returns>The selected insertion position.</returns>
    private static int FindInsertPosition(CompilationUnitSyntax root, int index)
    {
        var method = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<MethodDeclarationSyntax>(root, index);
        var summary = DocumentationCodeFixBenchmarkHelper.GetSummary(method);
        if (!XmlDocumentationHelper.NeedsTerminalPeriod(summary, out var position))
        {
            throw new InvalidOperationException("Expected a summary missing a terminal period.");
        }

        return position;
    }
}
