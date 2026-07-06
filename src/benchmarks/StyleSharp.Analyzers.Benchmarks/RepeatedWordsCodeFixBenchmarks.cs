// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the repeated-word code-fix path (SST1658).</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class RepeatedWordsCodeFixBenchmarks
{
    /// <summary>The prepared benchmark document and representative repeated-word span.</summary>
    private DirectCodeFixBenchmarkContext<TextSpan> _context = null!;

    /// <summary>Gets or sets the synthetic member count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Builds the benchmark document and selects one representative repeated-word span.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
        => _context = await DirectCodeFixBenchmarkHelper.CreateAsync(
            Nodes,
            static count => RepeatedWordsBenchmarkSource.Generate(count, violating: true),
            static (_, root, index) => Task.FromResult(FindWordSpan(root, index))).ConfigureAwait(false);

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>Benchmarks removing one representative repeated word.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> RepeatedWords_ApplyFixAsync()
    {
        var updated = await Sst1658NoRepeatedWordsCodeFixProvider.RemoveRepeatedWordAsync(_context.Document, _context.Target, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Finds the span of the second occurrence of the repeated word in the selected member's summary.</summary>
    /// <param name="root">The benchmark syntax root.</param>
    /// <param name="index">The zero-based method index to select.</param>
    /// <returns>The selected repeated word's span.</returns>
    private static TextSpan FindWordSpan(CompilationUnitSyntax root, int index)
    {
        const string Pattern = "the the";
        const int SecondWordOffset = 4;
        const int RepeatedWordLength = 3;

        var method = CodeFixBenchmarkSyntaxLookup.GetNthTypeMember<MethodDeclarationSyntax>(root, index);
        var summary = DocumentationCodeFixBenchmarkHelper.GetSummary(method);
        foreach (var node in summary.Content)
        {
            if (node is not XmlTextSyntax text)
            {
                continue;
            }

            foreach (var token in text.TextTokens)
            {
                var match = token.ValueText.IndexOf(Pattern, StringComparison.Ordinal);
                if (match >= 0)
                {
                    // The second word starts after "the " within the matched pattern.
                    return new TextSpan(token.SpanStart + match + SecondWordOffset, RepeatedWordLength);
                }
            }
        }

        throw new InvalidOperationException("Expected a repeated word in the summary.");
    }
}
