// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the property-summary code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PropertySummaryCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative property-summary target.</summary>
    private DirectCodeFixBenchmarkContext<(XmlElementSyntax Summary, string Prefix)> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative property summary.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            DocumentationCodeFixBenchmarkSource.GeneratePropertySummary,
            static (_, root, index) => Task.FromResult(FindTarget(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks prefixing one representative property summary with its accessor phrase.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> PropertySummary_ApplyFixAsync()
    {
        var updated = await PropertySummaryCodeFixProvider.ApplyAsync(_context.Document, _context.Target.Summary, _context.Target.Prefix, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative property-summary target in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based property index to select.</param>
    /// <returns>The selected summary element and accessor prefix.</returns>
    private static (XmlElementSyntax Summary, string Prefix) FindTarget(CompilationUnitSyntax root, int index)
    {
        var property = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<PropertyDeclarationSyntax>(root, index);
        return (DocumentationCodeFixBenchmarkHelper.GetSummary(property), DocumentationConventions.PropertyAccessorPrefix(property));
    }
}
