// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for the SST1110 code-fix path.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class OpeningParenOnDeclarationLineCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle member.</summary>
    private const int MiddleMemberDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The stable span of the target opening token.</summary>
    private TextSpan _openingSpan;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>The SST1110 code-fix benchmark shapes.</summary>
    public enum Shape
    {
        /// <summary>Method parameter list opening parenthesis.</summary>
        MethodParameter,

        /// <summary>Bracketed argument list opening bracket.</summary>
        BracketedArgument
    }

    /// <summary>Gets or sets the synthetic member count used for the benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Members { get; set; }

    /// <summary>Gets or sets the violating shape under test.</summary>
    [Params(
        Shape.MethodParameter,
        Shape.BracketedArgument)]
    public Shape CurrentShape { get; set; }

    /// <summary>Builds the benchmark document once per parameter set.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            OpeningParenOnDeclarationLineCodeFixBenchmarkSource.Generate(Members, CurrentShape == Shape.BracketedArgument));

        var root = (CompilationUnitSyntax)(await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var type = (TypeDeclarationSyntax)((BaseNamespaceDeclarationSyntax)root.Members[0]).Members[0];
        var targetMember = type.Members[Members / MiddleMemberDivisor];
        _openingSpan = FindOpeningSpan(targetMember, CurrentShape);
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

    /// <summary>Benchmarks applying the SST1110 code fix to one representative violation.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> OpeningParenOnDeclarationLine_ApplyFixAsync()
    {
        var updated = await OpeningParenOnDeclarationLineCodeFixProvider.FixAsync(_document, _openingSpan, CancellationToken.None).ConfigureAwait(false);
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

    /// <summary>Finds the opening-token span for the target member and benchmark shape.</summary>
    /// <param name="member">The target member.</param>
    /// <param name="shape">The violating shape under test.</param>
    /// <returns>The opening-token span.</returns>
    private static TextSpan FindOpeningSpan(
        MemberDeclarationSyntax member,
        Shape shape)
    {
        return shape switch
        {
            Shape.MethodParameter
                => ((MethodDeclarationSyntax)member).ParameterList.OpenParenToken.Span,
            _ => ((ElementAccessExpressionSyntax)((MethodDeclarationSyntax)member).ExpressionBody!.Expression).ArgumentList.OpenBracketToken.Span
        };
    }
}
