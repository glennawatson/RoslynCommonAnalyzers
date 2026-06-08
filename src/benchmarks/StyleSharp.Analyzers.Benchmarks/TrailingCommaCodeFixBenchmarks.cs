// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the trailing-comma code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class TrailingCommaCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative field declaration.</summary>
    private StructuralCodeFixBenchmarkContext<FieldDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative initializer without a trailing comma.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            StructuralCodeFixBenchmarkSource.GenerateTrailingComma,
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<FieldDeclarationSyntax>(root, index)).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the trailing-comma fix to one representative initializer.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> TrailingComma_ApplyFixAsync()
    {
        var initializer = (InitializerExpressionSyntax)((ImplicitArrayCreationExpressionSyntax)_context.Node.Declaration.Variables[0].Initializer!.Value).Initializer!;
        var position = initializer.Expressions[initializer.Expressions.Count - 1].Span.End;
        var updated = await Sst1413TrailingCommaCodeFixProvider.AddCommaAsync(_context.Document, position, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
