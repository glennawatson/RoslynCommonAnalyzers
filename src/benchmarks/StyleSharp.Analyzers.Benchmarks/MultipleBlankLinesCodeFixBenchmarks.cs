// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the multiple-blank-lines code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MultipleBlankLinesCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative extra-blank-line span.</summary>
    private DirectCodeFixBenchmarkContext<TextSpan> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative extra blank line.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateMultipleBlankLines,
            static async (document, root, index) =>
            {
                var text = await document.GetTextAsync().ConfigureAwait(false);
                var field = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<FieldDeclarationSyntax>(root, index == 0 ? 1 : index);
                var fieldLine = text.Lines.GetLineFromPosition(field.SpanStart).LineNumber;
                var blankLine = text.Lines[fieldLine - 2];
                return TextSpan.FromBounds(blankLine.Start, blankLine.EndIncludingLineBreak);
            }).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks removing one representative extra blank line.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> MultipleBlankLines_ApplyFixAsync()
    {
        var updated = await MultipleBlankLinesCodeFixProvider.RemoveAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }
}
