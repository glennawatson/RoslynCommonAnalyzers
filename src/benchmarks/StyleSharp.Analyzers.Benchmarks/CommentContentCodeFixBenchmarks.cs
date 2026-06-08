// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the comment-content code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CommentContentCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative empty-comment span.</summary>
    private DirectCodeFixBenchmarkContext<TextSpan> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative empty comment.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            LayoutTriviaCodeFixBenchmarkSource.GenerateCommentContent,
            static (_, root, index) => Task.FromResult(FindCommentSpan(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks applying the empty-comment fix to one representative comment.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> CommentContent_ApplyFixAsync()
    {
        var updated = await Sst1120CommentContentCodeFixProvider.RemoveAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the representative empty-comment span in the benchmark root.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based comment index to select.</param>
    /// <returns>The selected comment span.</returns>
    private static TextSpan FindCommentSpan(CompilationUnitSyntax root, int index)
    {
        var current = 0;
        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                continue;
            }

            if (current == index)
            {
                return trivia.Span;
            }

            current++;
        }

        throw new InvalidOperationException("Comment trivia not found.");
    }
}
