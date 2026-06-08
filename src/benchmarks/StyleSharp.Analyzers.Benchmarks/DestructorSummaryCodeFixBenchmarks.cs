// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the destructor-summary code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DestructorSummaryCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative destructor-summary target.</summary>
    private DirectCodeFixBenchmarkContext<(XmlElementSyntax Summary, string StandardSummary)> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative destructor summary.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Types,
            DocumentationCodeFixBenchmarkSource.GenerateDestructorSummary,
            static (_, root, index) => Task.FromResult(FindTarget(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks rewriting one representative destructor summary to the standard text.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> DestructorSummary_ApplyFixAsync()
    {
        var updated = await DestructorSummaryCodeFixProvider.ApplyAsync(_context.Document, _context.Target.Summary, _context.Target.StandardSummary, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative destructor-summary target in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based type index to select.</param>
    /// <returns>The selected summary element and standard replacement text.</returns>
    private static (XmlElementSyntax Summary, string StandardSummary) FindTarget(CompilationUnitSyntax root, int index)
    {
        var type = CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(root, index);
        var destructor = (DestructorDeclarationSyntax)type.Members[0];
        return (DocumentationCodeFixBenchmarkHelper.GetSummary(destructor), DocumentationConventions.DestructorStandardSummary(type));
    }
}
