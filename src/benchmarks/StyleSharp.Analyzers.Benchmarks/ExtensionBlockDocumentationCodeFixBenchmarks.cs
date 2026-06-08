// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for inserting an extension-block <c>&lt;param&gt;</c> stub (SST1655).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ExtensionBlockDocumentationCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative extension-block target.</summary>
    private DirectCodeFixBenchmarkContext<(TypeDeclarationSyntax Block, string Element)> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative undocumented receiver parameter.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            ExtensionBlockDocumentationBenchmarkSource.GenerateUndocumentedParameter,
            static (_, root, index) => Task.FromResult(FindTarget(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks inserting one representative extension-block parameter scaffold.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> ExtensionBlockDocumentationStub_ApplyFixAsync()
    {
        var updated = await DocumentationStubCodeFixProvider.InsertElementAsync(_context.Document, _context.Target.Block, _context.Target.Element, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative extension block and the parameter stub to insert.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based container index to select.</param>
    /// <returns>The selected extension block and documentation element text.</returns>
    private static (TypeDeclarationSyntax Block, string Element) FindTarget(CompilationUnitSyntax root, int index)
    {
        var container = (ClassDeclarationSyntax)root.Members[index];
        foreach (var member in container.Members)
        {
            if (member is TypeDeclarationSyntax block
                && ExtensionBlockHelper.IsExtensionBlock(block)
                && block.ParameterList?.Parameters is { Count: > 0 } parameters)
            {
                return (block, "<param name=\"" + parameters[0].Identifier.ValueText + "\"></param>");
            }
        }

        throw new InvalidOperationException("No extension block was generated for the benchmark.");
    }
}
