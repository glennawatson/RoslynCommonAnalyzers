// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the documentation-stub code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DocumentationStubCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative documentation-stub target.</summary>
    private DirectCodeFixBenchmarkContext<(MethodDeclarationSyntax Member, string Element)> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative undocumented parameter.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            DocumentationCodeFixBenchmarkSource.GenerateDocumentationStub,
            static (_, root, index) => Task.FromResult(FindTarget(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks inserting one representative documentation element scaffold.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> DocumentationStub_ApplyFixAsync()
    {
        var updated = await DocumentationStubCodeFixProvider.InsertElementAsync(_context.Document, _context.Target.Member, _context.Target.Element, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative documentation-stub target in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based method index to select.</param>
    /// <returns>The selected method and documentation element text.</returns>
    private static (MethodDeclarationSyntax Member, string Element) FindTarget(CompilationUnitSyntax root, int index)
    {
        var method = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<MethodDeclarationSyntax>(root, index);
        var parameter = method.ParameterList.Parameters[0];
        return (method, "<param name=\"" + parameter.Identifier.ValueText + "\"></param>");
    }
}
