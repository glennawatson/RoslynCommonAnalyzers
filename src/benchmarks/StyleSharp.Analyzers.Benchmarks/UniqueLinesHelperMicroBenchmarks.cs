// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Micro-benchmark that isolates the shared jagged-layout token walk
/// (<see cref="ArgumentsOrParameterOnSameLineHelper.ReportsJaggedLayout{T}"/>) from
/// Roslyn binding and the analyzer driver. The synthetic corpus is parsed and the
/// parameter lists extracted once in <see cref="Setup"/>, so the timed methods only
/// measure the per-list scan over pre-parsed nodes — the same isolation strategy
/// <see cref="LineScanBenchmarks"/> applies to a single list.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class UniqueLinesHelperMicroBenchmarks
{
    /// <summary>The parameter lists from the clean (single-line) corpus.</summary>
    private ParameterListSyntax[] _cleanLists = null!;

    /// <summary>The parameter lists from the violating (jagged multi-line) corpus.</summary>
    private ParameterListSyntax[] _violatingLists = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Parses both corpora and extracts their parameter lists once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _cleanLists = ExtractParameterLists(UniqueLinesBenchmarkSource.GenerateMethodDeclarationParameters(Nodes, violating: false));
        _violatingLists = ExtractParameterLists(UniqueLinesBenchmarkSource.GenerateMethodDeclarationParameters(Nodes, violating: true));
    }

    /// <summary>Scans the clean corpus, where the helper should report no jagged layouts.</summary>
    /// <returns>The number of lists reported as jagged.</returns>
    [Benchmark]
    public int Clean() => CountJagged(_cleanLists);

    /// <summary>Scans the violating corpus, where the helper should report every list as jagged.</summary>
    /// <returns>The number of lists reported as jagged.</returns>
    [Benchmark]
    public int Violating() => CountJagged(_violatingLists);

    /// <summary>Parses one synthetic source and collects its method parameter lists.</summary>
    /// <param name="source">The synthetic source text to parse.</param>
    /// <returns>The parameter lists found in the parsed tree.</returns>
    private static ParameterListSyntax[] ExtractParameterLists(string source)
    {
        var root = BenchmarkCompilationFactory.Parse(source).GetRoot();
        return [.. root.DescendantNodes().OfType<MethodDeclarationSyntax>().Select(static method => method.ParameterList)];
    }

    /// <summary>Calls the shared helper directly over each pre-parsed list and counts the jagged ones.</summary>
    /// <param name="lists">The pre-parsed parameter lists to scan.</param>
    /// <returns>The number of lists the helper reports as jagged.</returns>
    private static int CountJagged(ParameterListSyntax[] lists)
    {
        var jagged = 0;
        foreach (var list in lists)
        {
            if (ArgumentsOrParameterOnSameLineHelper.ReportsJaggedLayout(list, list.Parameters))
            {
                jagged++;
            }
        }

        return jagged;
    }
}
