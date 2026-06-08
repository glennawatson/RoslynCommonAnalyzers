// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the spacing code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class SpacingCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative spacing-violation span.</summary>
    private DirectCodeFixBenchmarkContext<TextSpan> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative multiple-whitespace violation.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateSpacing,
            static (_, root, index) => Task.FromResult(FindWhitespaceSpan(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the spacing fix to one representative double-space span.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> Spacing_ApplyFixAsync()
    {
        var updated = await SpacingCodeFixProvider.FixAsync(_context.Document, SpacingRules.MultipleWhitespace.Id, _context.Target, action: null, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative multiple-whitespace span in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based field index to select.</param>
    /// <returns>The selected whitespace span.</returns>
    private static TextSpan FindWhitespaceSpan(CompilationUnitSyntax root, int index)
    {
        var field = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<FieldDeclarationSyntax>(root, index);
        return TextSpan.FromBounds(field.Modifiers[field.Modifiers.Count - 1].Span.End, field.Declaration.Type.SpanStart);
    }
}
