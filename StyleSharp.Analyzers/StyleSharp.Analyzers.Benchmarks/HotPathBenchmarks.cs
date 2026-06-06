// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for the hot-path analyzer benchmarks.</summary>
public abstract class HotPathBenchmarkBase
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> SpacingAnalyzers = [new SpacingAnalyzer()];
    private static readonly ImmutableArray<DiagnosticAnalyzer> TupleElementNameAnalyzers = [new TupleElementNameAnalyzer()];
    private static readonly ImmutableArray<DiagnosticAnalyzer> ArgumentGuardAnalyzers = [new ArgumentGuardAnalyzer()];

    private ParameterListSyntax _lineCleanList = null!;
    private ParameterListSyntax _lineViolatingList = null!;
    private MemberAccessExpressionSyntax[] _tupleCleanNodes = null!;
    private MemberAccessExpressionSyntax[] _tupleViolatingNodes = null!;
    private SemanticModel _tupleCleanModel = null!;
    private SemanticModel _tupleViolatingModel = null!;
    private CSharpCompilation _tupleCleanCompilation = null!;
    private CSharpCompilation _tupleViolatingCompilation = null!;
    private ObjectCreationExpressionSyntax[] _nameofCleanNodes = null!;
    private ObjectCreationExpressionSyntax[] _nameofViolatingNodes = null!;
    private IfStatementSyntax[] _guardCleanNodes = null!;
    private IfStatementSyntax[] _guardViolatingNodes = null!;
    private ArgumentGuardAnalyzer.GuardHelpers _guardHelpers;
    private CSharpCompilation _guardCleanCompilation = null!;
    private CSharpCompilation _guardViolatingCompilation = null!;
    private CSharpCompilation _spacingCleanCompilation = null!;
    private CSharpCompilation _spacingViolatingCompilation = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(100, 1000)]
    public int Nodes { get; set; }

    /// <summary>Builds every benchmark corpus once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupLineScan();
        SetupTupleElementName();
        SetupUseNameof();
        SetupArgumentGuard();
        SetupSpacing();
    }

    /// <summary>Benchmarks the clean path of the jagged-list helper.</summary>
    /// <returns>The number of detected jagged layouts.</returns>
    protected int RunLineScanClean()
    {
        var count = 0;
        for (var i = 0; i < Nodes; i++)
        {
            if (ArgumentsOrParameterOnSameLineHelper.ReportsJaggedLayout(_lineCleanList, _lineCleanList.Parameters))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Benchmarks the violating path of the jagged-list helper.</summary>
    /// <returns>The number of detected jagged layouts.</returns>
    protected int RunLineScanViolating()
    {
        var count = 0;
        for (var i = 0; i < Nodes; i++)
        {
            if (ArgumentsOrParameterOnSameLineHelper.ReportsJaggedLayout(_lineViolatingList, _lineViolatingList.Parameters))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Benchmarks the clean path of SST1142.</summary>
    /// <returns>The number of tuple element rewrites found.</returns>
    protected int RunTupleClean()
    {
        var count = 0;
        for (var i = 0; i < _tupleCleanNodes.Length; i++)
        {
            if (TupleElementNameAnalyzer.TryGetReplacementName(_tupleCleanNodes[i], _tupleCleanModel, CancellationToken.None, out _))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Benchmarks the violating path of SST1142.</summary>
    /// <returns>The number of tuple element rewrites found.</returns>
    protected int RunTupleViolating()
    {
        var count = 0;
        for (var i = 0; i < _tupleViolatingNodes.Length; i++)
        {
            if (TupleElementNameAnalyzer.TryGetReplacementName(_tupleViolatingNodes[i], _tupleViolatingModel, CancellationToken.None, out _))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Benchmarks the clean path of SST1142 including diagnostic reporting.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunTupleAnalyzerClean()
        => _tupleCleanCompilation.WithAnalyzers(TupleElementNameAnalyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;

    /// <summary>Benchmarks the violating path of SST1142 including diagnostic reporting.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunTupleAnalyzerViolating()
        => _tupleViolatingCompilation.WithAnalyzers(TupleElementNameAnalyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;

    /// <summary>Benchmarks the clean path of SST1415.</summary>
    /// <returns>The number of constructor arguments that should become <c>nameof</c>.</returns>
    protected int RunUseNameofClean()
    {
        var count = 0;
        for (var i = 0; i < _nameofCleanNodes.Length; i++)
        {
            count += UseNameofAnalyzer.CountParameterNameLiteralMatches(_nameofCleanNodes[i]);
        }

        return count;
    }

    /// <summary>Benchmarks the violating path of SST1415.</summary>
    /// <returns>The number of constructor arguments that should become <c>nameof</c>.</returns>
    protected int RunUseNameofViolating()
    {
        var count = 0;
        for (var i = 0; i < _nameofViolatingNodes.Length; i++)
        {
            count += UseNameofAnalyzer.CountParameterNameLiteralMatches(_nameofViolatingNodes[i]);
        }

        return count;
    }

    /// <summary>Benchmarks the clean path of the throw-helper matcher family.</summary>
    /// <returns>The number of matching guard-helper rewrites.</returns>
    protected int RunArgumentGuardClean()
    {
        var count = 0;
        for (var i = 0; i < _guardCleanNodes.Length; i++)
        {
            if (ArgumentGuardAnalyzer.WouldReportForBenchmark(_guardCleanNodes[i], _guardHelpers))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Benchmarks the violating path of the throw-helper matcher family.</summary>
    /// <returns>The number of matching guard-helper rewrites.</returns>
    protected int RunArgumentGuardViolating()
    {
        var count = 0;
        for (var i = 0; i < _guardViolatingNodes.Length; i++)
        {
            if (ArgumentGuardAnalyzer.WouldReportForBenchmark(_guardViolatingNodes[i], _guardHelpers))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Benchmarks the clean path of the throw-helper analyzer including diagnostic reporting.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunArgumentGuardAnalyzerClean()
        => _guardCleanCompilation.WithAnalyzers(ArgumentGuardAnalyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;

    /// <summary>Benchmarks the violating path of the throw-helper analyzer including diagnostic reporting.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunArgumentGuardAnalyzerViolating()
        => _guardViolatingCompilation.WithAnalyzers(ArgumentGuardAnalyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;

    /// <summary>Benchmarks the clean path of the spacing analyzer's token walk.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunSpacingClean()
        => _spacingCleanCompilation.WithAnalyzers(SpacingAnalyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;

    /// <summary>Benchmarks the violating path of the spacing analyzer's token walk.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected int RunSpacingViolating()
        => _spacingViolatingCompilation.WithAnalyzers(SpacingAnalyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Length;

    private void SetupLineScan()
    {
        const string source = """
            class C
            {
                void Clean(
                    string a,
                    int b,
                    bool c,
                    long d) { }

                void Violating(string a, int b,
                    bool c, long d) { }
            }
            """;

        var tree = BenchmarkCompilationFactory.Parse(source);
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        _lineCleanList = methods[0].ParameterList;
        _lineViolatingList = methods[1].ParameterList;
    }

    private void SetupTupleElementName()
    {
        var (cleanTree, cleanCompilation) = BenchmarkCompilationFactory.CreateCompilation(TupleElementNameBenchmarkSource.Generate(Nodes, violating: false));
        var (violatingTree, violatingCompilation) = BenchmarkCompilationFactory.CreateCompilation(TupleElementNameBenchmarkSource.Generate(Nodes, violating: true));
        _tupleCleanCompilation = cleanCompilation;
        _tupleViolatingCompilation = violatingCompilation;
        _tupleCleanModel = cleanCompilation.GetSemanticModel(cleanTree);
        _tupleViolatingModel = violatingCompilation.GetSemanticModel(violatingTree);
        _tupleCleanNodes = cleanTree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().ToArray();
        _tupleViolatingNodes = violatingTree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().ToArray();
    }

    private void SetupUseNameof()
    {
        var cleanTree = BenchmarkCompilationFactory.Parse(UseNameofBenchmarkSource.Generate(Nodes, violating: false));
        var violatingTree = BenchmarkCompilationFactory.Parse(UseNameofBenchmarkSource.Generate(Nodes, violating: true));
        _nameofCleanNodes = cleanTree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>().ToArray();
        _nameofViolatingNodes = violatingTree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>().ToArray();
    }

    private void SetupArgumentGuard()
    {
        var (cleanTree, cleanCompilation) = BenchmarkCompilationFactory.CreateCompilation(ArgumentGuardBenchmarkSource.Generate(Nodes, violating: false));
        var (violatingTree, violatingCompilation) = BenchmarkCompilationFactory.CreateCompilation(ArgumentGuardBenchmarkSource.Generate(Nodes, violating: true));
        _guardCleanCompilation = cleanCompilation;
        _guardViolatingCompilation = violatingCompilation;
        _guardCleanNodes = cleanTree.GetRoot().DescendantNodes().OfType<IfStatementSyntax>().ToArray();
        _guardViolatingNodes = violatingTree.GetRoot().DescendantNodes().OfType<IfStatementSyntax>().ToArray();
        _guardHelpers = ArgumentGuardAnalyzer.CreateBenchmarkHelpers();
    }

    private void SetupSpacing()
    {
        (_, _spacingCleanCompilation) = BenchmarkCompilationFactory.CreateCompilation(SpacingBenchmarkSource.Generate(Nodes, violating: false));
        (_, _spacingViolatingCompilation) = BenchmarkCompilationFactory.CreateCompilation(SpacingBenchmarkSource.Generate(Nodes, violating: true));
    }
}

/// <summary>Memory/allocation benchmarks for the hottest analyzer pipelines.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class HotPathBenchmarks : HotPathBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public int LineScan_Clean() => RunLineScanClean();

    [Benchmark]
    public int LineScan_Violating() => RunLineScanViolating();

    [Benchmark]
    public int TupleElementName_Clean() => RunTupleClean();

    [Benchmark]
    public int TupleElementName_Violating() => RunTupleViolating();

    [Benchmark]
    public int TupleElementNameAnalyzer_Clean() => RunTupleAnalyzerClean();

    [Benchmark]
    public int TupleElementNameAnalyzer_Violating() => RunTupleAnalyzerViolating();

    [Benchmark]
    public int UseNameof_Clean() => RunUseNameofClean();

    [Benchmark]
    public int UseNameof_Violating() => RunUseNameofViolating();

    [Benchmark]
    public int ArgumentGuard_Clean() => RunArgumentGuardClean();

    [Benchmark]
    public int ArgumentGuard_Violating() => RunArgumentGuardViolating();

    [Benchmark]
    public int ArgumentGuardAnalyzer_Clean() => RunArgumentGuardAnalyzerClean();

    [Benchmark]
    public int ArgumentGuardAnalyzer_Violating() => RunArgumentGuardAnalyzerViolating();

    [Benchmark]
    public int Spacing_Clean() => RunSpacingClean();

    [Benchmark]
    public int Spacing_Violating() => RunSpacingViolating();
}

/// <summary>Allocation-profile benchmarks for the hottest analyzer pipelines.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class HotPathProfiledAllocBenchmarks : HotPathBenchmarkBase
{
    [Benchmark]
    public int LineScan_Clean() => RunLineScanClean();

    [Benchmark]
    public int LineScan_Violating() => RunLineScanViolating();

    [Benchmark]
    public int TupleElementName_Clean() => RunTupleClean();

    [Benchmark]
    public int TupleElementName_Violating() => RunTupleViolating();

    [Benchmark]
    public int TupleElementNameAnalyzer_Clean() => RunTupleAnalyzerClean();

    [Benchmark]
    public int TupleElementNameAnalyzer_Violating() => RunTupleAnalyzerViolating();

    [Benchmark]
    public int UseNameof_Clean() => RunUseNameofClean();

    [Benchmark]
    public int UseNameof_Violating() => RunUseNameofViolating();

    [Benchmark]
    public int ArgumentGuard_Clean() => RunArgumentGuardClean();

    [Benchmark]
    public int ArgumentGuard_Violating() => RunArgumentGuardViolating();

    [Benchmark]
    public int ArgumentGuardAnalyzer_Clean() => RunArgumentGuardAnalyzerClean();

    [Benchmark]
    public int ArgumentGuardAnalyzer_Violating() => RunArgumentGuardAnalyzerViolating();

    [Benchmark]
    public int Spacing_Clean() => RunSpacingClean();

    [Benchmark]
    public int Spacing_Violating() => RunSpacingViolating();
}

/// <summary>CPU-sample benchmarks for the hottest analyzer pipelines.</summary>
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class HotPathProfiledCpuBenchmarks : HotPathBenchmarkBase
{
    [Benchmark]
    public int LineScan_Clean() => RunLineScanClean();

    [Benchmark]
    public int LineScan_Violating() => RunLineScanViolating();

    [Benchmark]
    public int TupleElementName_Clean() => RunTupleClean();

    [Benchmark]
    public int TupleElementName_Violating() => RunTupleViolating();

    [Benchmark]
    public int TupleElementNameAnalyzer_Clean() => RunTupleAnalyzerClean();

    [Benchmark]
    public int TupleElementNameAnalyzer_Violating() => RunTupleAnalyzerViolating();

    [Benchmark]
    public int UseNameof_Clean() => RunUseNameofClean();

    [Benchmark]
    public int UseNameof_Violating() => RunUseNameofViolating();

    [Benchmark]
    public int ArgumentGuard_Clean() => RunArgumentGuardClean();

    [Benchmark]
    public int ArgumentGuard_Violating() => RunArgumentGuardViolating();

    [Benchmark]
    public int ArgumentGuardAnalyzer_Clean() => RunArgumentGuardAnalyzerClean();

    [Benchmark]
    public int ArgumentGuardAnalyzer_Violating() => RunArgumentGuardAnalyzerViolating();

    [Benchmark]
    public int Spacing_Clean() => RunSpacingClean();

    [Benchmark]
    public int Spacing_Violating() => RunSpacingViolating();
}
