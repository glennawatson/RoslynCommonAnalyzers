// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>
/// Micro-benchmark proving the allocation/time delta between the old set-based
/// jagged-list decision and the new single-pass scan. Both methods mirror the
/// production logic; the optimized one matches
/// <c>ArgumentsOrParameterOnSameLineHelper.Analyze</c>.
/// </summary>
[MemoryDiagnoser]
public class LineScanBenchmarks
{
    /// <summary>The parsed syntax tree for the current scenario.</summary>
    private SyntaxTree _tree = null!;

    /// <summary>The parameter list selected for the current scenario.</summary>
    private ParameterListSyntax _list = null!;

    /// <summary>The layouts a comma-delimited list can take.</summary>
    public enum Layout
    {
        /// <summary>All items on a single line (valid).</summary>
        OneLine,

        /// <summary>Each item on its own line (valid).</summary>
        EachOwnLine,

        /// <summary>A mix - some items share a line, others wrap (reported).</summary>
        Jagged
    }

    /// <summary>Gets or sets the parameter-list layout under test.</summary>
    [Params(Layout.OneLine, Layout.EachOwnLine, Layout.Jagged)]
    public Layout Scenario { get; set; }

    /// <summary>Parses the fixture and selects the parameter list for the current scenario.</summary>
    [GlobalSetup]
    public void Setup()
    {
        const string Source = @"
class C
{
    void OneLine(string a, int b, bool c, long d, double e, char f) { }
    void EachOwnLine(
        string a,
        int b,
        bool c,
        long d,
        double e,
        char f) { }
    void Jagged(string a, int b,
        bool c, long d,
        double e, char f) { }
}";
        _tree = CSharpSyntaxTree.ParseText(Source);
        var members = ((ClassDeclarationSyntax)((CompilationUnitSyntax)_tree.GetRoot()).Members[0]).Members;
        var methodIndex = Scenario switch
        {
            Layout.OneLine => 0,
            Layout.EachOwnLine => 1,
            _ => 2
        };

        _list = ((MethodDeclarationSyntax)members[methodIndex]).ParameterList;
    }

    /// <summary>The original HashSet + LINQ + per-item Location approach.</summary>
    /// <returns>Whether the list would be reported as jagged.</returns>
    [Benchmark(Baseline = true)]
    public bool Baseline_HashSetLinq() => BaselineReports(_list, _list.Parameters);

    /// <summary>The new allocation-free single-pass scan.</summary>
    /// <returns>Whether the list would be reported as jagged.</returns>
    [Benchmark]
    public bool Optimized_ManualScan() => OptimizedReports(_tree, _list, _list.Parameters);

    /// <summary>Mirrors the original set-based implementation.</summary>
    /// <typeparam name="T">The syntax-node type in the separated list.</typeparam>
    /// <param name="listNode">The list container node.</param>
    /// <param name="list">The separated list to inspect.</param>
    /// <returns>Whether the list would be reported as jagged.</returns>
    private static bool BaselineReports<T>(SyntaxNode listNode, SeparatedSyntaxList<T> list)
        where T : SyntaxNode
    {
        if (list.Count <= 1)
        {
            return false;
        }

        var parameterLine = listNode.GetLocation().GetLineSpan().StartLinePosition.Line;
        var diffChecker = new HashSet<int> { parameterLine };
        var lineNumbers = list.Select(static x => x.GetLocation().GetLineSpan().StartLinePosition.Line).ToList();
        diffChecker.UnionWith(lineNumbers);

        return diffChecker.Count != list.Count + 1 && diffChecker.Count != 1;
    }

    /// <summary>Mirrors the optimized single-pass implementation.</summary>
    /// <typeparam name="T">The syntax-node type in the separated list.</typeparam>
    /// <param name="tree">The syntax tree containing the list.</param>
    /// <param name="listNode">The list container node.</param>
    /// <param name="list">The separated list to inspect.</param>
    /// <returns>Whether the list would be reported as jagged.</returns>
    private static bool OptimizedReports<T>(SyntaxTree tree, SyntaxNode listNode, SeparatedSyntaxList<T> list)
        where T : SyntaxNode
    {
        var count = list.Count;
        if (count <= 1)
        {
            return false;
        }

        var previousLine = tree.GetLineSpan(listNode.Span).StartLinePosition.Line;
        var sawShared = false;
        var sawSeparated = false;

        for (var index = 0; index < list.Count; index++)
        {
            var item = list[index];
            var line = tree.GetLineSpan(item.Span).StartLinePosition.Line;
            if (line == previousLine)
            {
                sawShared = true;
            }
            else
            {
                sawSeparated = true;
            }

            if (sawShared && sawSeparated)
            {
                return true;
            }

            previousLine = line;
        }

        return false;
    }
}
