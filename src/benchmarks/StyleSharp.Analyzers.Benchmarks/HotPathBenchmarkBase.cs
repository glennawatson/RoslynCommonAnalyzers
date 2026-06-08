// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared setup for the hot-path analyzer benchmarks.</summary>
public abstract class HotPathBenchmarkBase
{
    /// <summary>The spacing analyzers used by the hot-path suites.</summary>
    private static readonly ImmutableArray<DiagnosticAnalyzer> SpacingAnalyzers = [new SpacingAnalyzer()];

    /// <summary>The tuple-element analyzers used by the hot-path suites.</summary>
    private static readonly ImmutableArray<DiagnosticAnalyzer> TupleElementNameAnalyzers = [new TupleElementNameAnalyzer()];

    /// <summary>The argument-guard analyzers used by the hot-path suites.</summary>
    private static readonly ImmutableArray<DiagnosticAnalyzer> ArgumentGuardAnalyzers = [new ArgumentGuardAnalyzer()];

    /// <summary>The parsed clean parameter list used by the line-scan benchmark.</summary>
    private ParameterListSyntax _lineCleanList = null!;

    /// <summary>The parsed violating parameter list used by the line-scan benchmark.</summary>
    private ParameterListSyntax _lineViolatingList = null!;

    /// <summary>The clean tuple member-access nodes.</summary>
    private MemberAccessExpressionSyntax[] _tupleCleanNodes = null!;

    /// <summary>The violating tuple member-access nodes.</summary>
    private MemberAccessExpressionSyntax[] _tupleViolatingNodes = null!;

    /// <summary>The semantic model for the clean tuple benchmark compilation.</summary>
    private SemanticModel _tupleCleanModel = null!;

    /// <summary>The semantic model for the violating tuple benchmark compilation.</summary>
    private SemanticModel _tupleViolatingModel = null!;

    /// <summary>The clean tuple benchmark compilation.</summary>
    private CSharpCompilation _tupleCleanCompilation = null!;

    /// <summary>The violating tuple benchmark compilation.</summary>
    private CSharpCompilation _tupleViolatingCompilation = null!;

    /// <summary>The clean <c>nameof</c> object-creation nodes.</summary>
    private ObjectCreationExpressionSyntax[] _nameofCleanNodes = null!;

    /// <summary>The violating <c>nameof</c> object-creation nodes.</summary>
    private ObjectCreationExpressionSyntax[] _nameofViolatingNodes = null!;

    /// <summary>The clean argument-guard <c>if</c> statements.</summary>
    private IfStatementSyntax[] _guardCleanNodes = null!;

    /// <summary>The violating argument-guard <c>if</c> statements.</summary>
    private IfStatementSyntax[] _guardViolatingNodes = null!;

    /// <summary>The cached helper set used by the argument-guard benchmark.</summary>
    private ArgumentGuardAnalyzer.GuardHelpers _guardHelpers;

    /// <summary>The clean argument-guard benchmark compilation.</summary>
    private CSharpCompilation _guardCleanCompilation = null!;

    /// <summary>The violating argument-guard benchmark compilation.</summary>
    private CSharpCompilation _guardViolatingCompilation = null!;

    /// <summary>The clean spacing benchmark compilation.</summary>
    private CSharpCompilation _spacingCleanCompilation = null!;

    /// <summary>The violating spacing benchmark compilation.</summary>
    private CSharpCompilation _spacingViolatingCompilation = null!;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
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
    protected Task<int> RunTupleAnalyzerCleanAsync()
        => HotPathBenchmarkRunner.GetDiagnosticCountAsync(_tupleCleanCompilation, TupleElementNameAnalyzers);

    /// <summary>Benchmarks the violating path of SST1142 including diagnostic reporting.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected Task<int> RunTupleAnalyzerViolatingAsync()
        => HotPathBenchmarkRunner.GetDiagnosticCountAsync(_tupleViolatingCompilation, TupleElementNameAnalyzers);

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
    protected Task<int> RunArgumentGuardAnalyzerCleanAsync()
        => HotPathBenchmarkRunner.GetDiagnosticCountAsync(_guardCleanCompilation, ArgumentGuardAnalyzers);

    /// <summary>Benchmarks the violating path of the throw-helper analyzer including diagnostic reporting.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected Task<int> RunArgumentGuardAnalyzerViolatingAsync()
        => HotPathBenchmarkRunner.GetDiagnosticCountAsync(_guardViolatingCompilation, ArgumentGuardAnalyzers);

    /// <summary>Benchmarks the clean path of the spacing analyzer's token walk.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected Task<int> RunSpacingCleanAsync()
        => HotPathBenchmarkRunner.GetDiagnosticCountAsync(_spacingCleanCompilation, SpacingAnalyzers);

    /// <summary>Benchmarks the violating path of the spacing analyzer's token walk.</summary>
    /// <returns>The number of diagnostics produced.</returns>
    protected Task<int> RunSpacingViolatingAsync()
        => HotPathBenchmarkRunner.GetDiagnosticCountAsync(_spacingViolatingCompilation, SpacingAnalyzers);

    /// <summary>Gets the method declarations from the benchmark's single top-level type.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The type's method declarations.</returns>
    private static MethodDeclarationSyntax[] GetTypeMethods(CompilationUnitSyntax root)
    {
        var members = ((TypeDeclarationSyntax)root.Members[0]).Members;
        var methods = new MethodDeclarationSyntax[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            methods[i] = (MethodDeclarationSyntax)members[i];
        }

        return methods;
    }

    /// <summary>Gets the tuple member-access expressions emitted in the benchmark method body.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The tuple member-access expressions.</returns>
    private static MemberAccessExpressionSyntax[] GetTupleMemberAccesses(CompilationUnitSyntax root)
    {
        var statements = GetSingleMethod(root).Body!.Statements;
        var accesses = new MemberAccessExpressionSyntax[statements.Count];
        for (var i = 0; i < statements.Count; i++)
        {
            accesses[i] = (MemberAccessExpressionSyntax)((InvocationExpressionSyntax)((ExpressionStatementSyntax)statements[i]).Expression)
                .ArgumentList.Arguments[0].Expression;
        }

        return accesses;
    }

    /// <summary>Gets the object-creation expressions emitted in the benchmark method body.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The object-creation expressions.</returns>
    private static ObjectCreationExpressionSyntax[] GetObjectCreations(CompilationUnitSyntax root)
    {
        var statements = GetSingleMethod(root).Body!.Statements;
        var objectCreations = new ObjectCreationExpressionSyntax[statements.Count - 2];
        for (var i = 1; i < statements.Count - 1; i++)
        {
            objectCreations[i - 1] = (ObjectCreationExpressionSyntax)((AssignmentExpressionSyntax)((ExpressionStatementSyntax)statements[i]).Expression).Right;
        }

        return objectCreations;
    }

    /// <summary>Gets the guard <c>if</c> statements emitted as the first statement in each benchmark method.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The guard <c>if</c> statements.</returns>
    private static IfStatementSyntax[] GetIfStatements(CompilationUnitSyntax root)
    {
        var methods = GetTypeMethods(root);
        var statements = new IfStatementSyntax[methods.Length];
        for (var i = 0; i < methods.Length; i++)
        {
            statements[i] = (IfStatementSyntax)methods[i].Body!.Statements[0];
        }

        return statements;
    }

    /// <summary>Gets the single benchmark method from a single-type compilation unit.</summary>
    /// <param name="root">The parsed compilation unit.</param>
    /// <returns>The single method declaration.</returns>
    private static MethodDeclarationSyntax GetSingleMethod(CompilationUnitSyntax root)
        => (MethodDeclarationSyntax)((TypeDeclarationSyntax)root.Members[0]).Members[0];

    /// <summary>Parses the jagged-line benchmark fixture.</summary>
    private void SetupLineScan()
    {
        const string Source = """
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

        var tree = BenchmarkCompilationFactory.Parse(Source);
        var methods = GetTypeMethods((CompilationUnitSyntax)tree.GetRoot());
        _lineCleanList = methods[0].ParameterList;
        _lineViolatingList = methods[1].ParameterList;
    }

    /// <summary>Builds the tuple-element benchmark corpora.</summary>
    private void SetupTupleElementName()
    {
        var (cleanTree, cleanCompilation) = BenchmarkCompilationFactory.CreateCompilation(TupleElementNameBenchmarkSource.Generate(Nodes, violating: false));
        var (violatingTree, violatingCompilation) = BenchmarkCompilationFactory.CreateCompilation(TupleElementNameBenchmarkSource.Generate(Nodes, violating: true));
        _tupleCleanCompilation = cleanCompilation;
        _tupleViolatingCompilation = violatingCompilation;
        _tupleCleanModel = cleanCompilation.GetSemanticModel(cleanTree);
        _tupleViolatingModel = violatingCompilation.GetSemanticModel(violatingTree);
        _tupleCleanNodes = GetTupleMemberAccesses((CompilationUnitSyntax)cleanTree.GetRoot());
        _tupleViolatingNodes = GetTupleMemberAccesses((CompilationUnitSyntax)violatingTree.GetRoot());
    }

    /// <summary>Builds the <c>nameof</c> benchmark corpora.</summary>
    private void SetupUseNameof()
    {
        var cleanTree = BenchmarkCompilationFactory.Parse(UseNameofBenchmarkSource.Generate(Nodes, violating: false));
        var violatingTree = BenchmarkCompilationFactory.Parse(UseNameofBenchmarkSource.Generate(Nodes, violating: true));
        _nameofCleanNodes = GetObjectCreations((CompilationUnitSyntax)cleanTree.GetRoot());
        _nameofViolatingNodes = GetObjectCreations((CompilationUnitSyntax)violatingTree.GetRoot());
    }

    /// <summary>Builds the argument-guard benchmark corpora.</summary>
    private void SetupArgumentGuard()
    {
        var (cleanTree, cleanCompilation) = BenchmarkCompilationFactory.CreateCompilation(ArgumentGuardBenchmarkSource.Generate(Nodes, violating: false));
        var (violatingTree, violatingCompilation) = BenchmarkCompilationFactory.CreateCompilation(ArgumentGuardBenchmarkSource.Generate(Nodes, violating: true));
        _guardCleanCompilation = cleanCompilation;
        _guardViolatingCompilation = violatingCompilation;
        _guardCleanNodes = GetIfStatements((CompilationUnitSyntax)cleanTree.GetRoot());
        _guardViolatingNodes = GetIfStatements((CompilationUnitSyntax)violatingTree.GetRoot());
        _guardHelpers = ArgumentGuardAnalyzer.CreateBenchmarkHelpers();
    }

    /// <summary>Builds the spacing benchmark corpora.</summary>
    private void SetupSpacing()
    {
        (_, _spacingCleanCompilation) = BenchmarkCompilationFactory.CreateCompilation(SpacingBenchmarkSource.Generate(Nodes, violating: false));
        (_, _spacingViolatingCompilation) = BenchmarkCompilationFactory.CreateCompilation(SpacingBenchmarkSource.Generate(Nodes, violating: true));
    }
}
