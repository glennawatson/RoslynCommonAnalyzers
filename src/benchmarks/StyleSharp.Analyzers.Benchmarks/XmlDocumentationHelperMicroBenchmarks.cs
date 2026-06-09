// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Micro-benchmark that isolates the XML documentation walks
/// (<see cref="XmlDocumentationHelper.HasText"/> / <see cref="XmlDocumentationHelper.NeedsTerminalPeriod"/> /
/// <see cref="XmlDocumentationHelper.GetElementName"/>) from Roslyn binding and the analyzer driver.
/// The documented members are parsed and their documentation comments extracted once in
/// <see cref="Setup"/>, so the timed method only measures the per-element token walks — the same
/// isolation strategy <see cref="UniqueLinesHelperMicroBenchmarks"/> applies to the jagged-layout helper.
/// This is the level at which the <c>DescendantTokens</c>-to-allocation-free rewrite is visible
/// (the end-to-end <see cref="MemberDocumentationBenchmarks"/> are dominated by compilation binding).
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class XmlDocumentationHelperMicroBenchmarks
{
    /// <summary>The documentation comments extracted from the parsed corpus.</summary>
    private DocumentationCommentTriviaSyntax[] _comments = null!;

    /// <summary>Gets or sets the synthetic member count used for the corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Parses the documented corpus and extracts every documentation comment once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var root = BenchmarkCompilationFactory.Parse(MemberDocumentationBenchmarkSource.Generate(Nodes, violating: false)).GetRoot();
        _comments = [.. root.DescendantNodes(descendIntoTrivia: true).OfType<DocumentationCommentTriviaSyntax>()];
    }

    /// <summary>Drives the per-element documentation walks over every pre-parsed comment.</summary>
    /// <returns>An accumulated count, returned so the walks are not optimized away.</returns>
    [Benchmark]
    public int ScanContent()
    {
        var count = 0;
        foreach (var comment in _comments)
        {
            foreach (var node in comment.Content)
            {
                if (XmlDocumentationHelper.GetElementName(node) is null)
                {
                    continue;
                }

                if (XmlDocumentationHelper.HasText(node))
                {
                    count++;
                }

                if (node is XmlElementSyntax element && XmlDocumentationHelper.NeedsTerminalPeriod(element, out _))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
