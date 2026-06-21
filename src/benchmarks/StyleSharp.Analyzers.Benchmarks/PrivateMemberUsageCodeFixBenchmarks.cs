// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the unused-private-member code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PrivateMemberUsageCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative unused method.</summary>
    private StructuralCodeFixBenchmarkContext<MethodDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative unused private method.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            static count => PrivateMemberUsageBenchmarkSource.Generate(count, violating: true),
            static (root, index) => (MethodDeclarationSyntax)CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(root, index).Members[1])
            .ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the unused-private-member fix to one representative method.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> PrivateMemberUsage_ApplyFixAsync()
    {
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveUnusedPrivateMember, _context.Node.Identifier.GetLocation(), "Unused");
        var updated = Sst1440PrivateMemberUsageCodeFixProvider.Apply(_context.Document, _context.Root, diagnostic);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
