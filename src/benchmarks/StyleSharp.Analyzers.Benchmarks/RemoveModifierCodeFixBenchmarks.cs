// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the remove-modifier code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class RemoveModifierCodeFixBenchmarks
{
    /// <summary>Stores the prepared benchmark document and representative class declaration.</summary>
    private StructuralCodeFixBenchmarkContext<ClassDeclarationSyntax> _context = null!;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document and selects one representative declaration with a redundant partial modifier.</summary>
    /// <returns>A task that completes when the benchmark context has been created.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await StructuralCodeFixBenchmarkHelper.CreateAsync(
            Types,
            StructuralCodeFixBenchmarkSource.GenerateRemoveModifier,
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthNamespaceMember<ClassDeclarationSyntax>(root, index)).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the remove-modifier fix to one representative declaration.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> RemoveModifier_ApplyFixAsync()
    {
        var updated = RemoveModifierCodeFixProvider.RemoveModifier(_context.Document, _context.Root, _context.Node, FindPartialModifier(_context.Node));
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the partial modifier token that the benchmark removes.</summary>
    /// <param name="declaration">The declaration whose modifiers are inspected.</param>
    /// <returns>The partial modifier token.</returns>
    private static SyntaxToken FindPartialModifier(ClassDeclarationSyntax declaration)
    {
        for (var i = 0; i < declaration.Modifiers.Count; i++)
        {
            if (declaration.Modifiers[i].IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
            {
                return declaration.Modifiers[i];
            }
        }

        throw new InvalidOperationException("Partial modifier not found.");
    }
}
