// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the SST1653 code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class SingleLineSummaryCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle summary element.</summary>
    private const int MiddleSummaryDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The stable span of the target summary element in the benchmark document.</summary>
    private TextSpan _summarySpan;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic type count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallTypeCount, BenchmarkParameterValues.LargeTypeCount)]
    public int Types { get; set; }

    /// <summary>Builds the benchmark document once per parameter set.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, SingleLineSummaryCodeFixBenchmarkSource.Generate(Types));

        var root = await _document.GetSyntaxRootAsync().ConfigureAwait(false);
        _summarySpan = FindTargetSummary(root!).Span;
    }

    /// <summary>Disposes the workspace created for the benchmark document.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Disposes the benchmark workspace.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Benchmarks applying the single-line-summary fix to one representative summary.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> SingleLineSummary_ApplyFixAsync()
    {
        var root = await _document.GetSyntaxRootAsync().ConfigureAwait(false);
        var node = root!.FindNode(_summarySpan, findInsideTrivia: true, getInnermostNodeForTie: true);
        var summary = node.FirstAncestorOrSelf<XmlElementSyntax>()!;
        var updated = await SingleLineSummaryCodeFixProvider.CollapseAsync(_document, summary, CancellationToken.None).ConfigureAwait(false);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Disposes managed state owned by the benchmark instance.</summary>
    /// <param name="disposing">Whether managed state should be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _workspace.Dispose();
        _disposed = true;
    }

    /// <summary>Finds the middle summary element in the benchmark document.</summary>
    /// <param name="root">The syntax root.</param>
    /// <returns>The selected summary element.</returns>
    private static XmlElementSyntax FindTargetSummary(SyntaxNode root)
    {
        var namespaceDeclaration = (BaseNamespaceDeclarationSyntax)((CompilationUnitSyntax)root).Members[0];
        var targetIndex = namespaceDeclaration.Members.Count / MiddleSummaryDivisor;
        for (var i = 0; i < namespaceDeclaration.Members.Count; i++)
        {
            if (i != targetIndex
                || namespaceDeclaration.Members[i] is not TypeDeclarationSyntax type
                || XmlDocumentationHelper.GetDocumentationComment(type) is not { } documentation
                || XmlDocumentationHelper.FindElement(documentation, "summary") is not XmlElementSyntax summary)
            {
                continue;
            }

            return summary;
        }

        throw new InvalidOperationException("Summary element not found.");
    }
}
