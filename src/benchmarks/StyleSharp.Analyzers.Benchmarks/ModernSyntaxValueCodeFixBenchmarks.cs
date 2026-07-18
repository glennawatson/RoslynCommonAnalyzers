// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Memory benchmarks for modern-syntax value code fixes.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ModernSyntaxValueCodeFixBenchmarks : IDisposable
{
    /// <summary>The divisor used to select the middle benchmark node.</summary>
    private const int MiddleNodeDivisor = 2;

    /// <summary>The workspace that owns the benchmark document.</summary>
    private AdhocWorkspace _workspace = null!;

    /// <summary>The prepared benchmark document.</summary>
    private Document _document = null!;

    /// <summary>The cached syntax root for the benchmark document.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The representative diagnostic passed to the code fix.</summary>
    private Diagnostic _diagnostic = null!;

    /// <summary>Tracks whether the benchmark instance has already been disposed.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the synthetic node count used for each benchmark corpus.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Gets or sets the modern-syntax value shape under test.</summary>
    [Params(
        ModernSyntaxValueBenchmarkShape.Interpolation,
        ModernSyntaxValueBenchmarkShape.IgnoredValue,
        ModernSyntaxValueBenchmarkShape.OverwrittenValue,
        ModernSyntaxValueBenchmarkShape.CoalesceAssignment,
        ModernSyntaxValueBenchmarkShape.AnonymousTuple,
        ModernSyntaxValueBenchmarkShape.ForeachCast,
        ModernSyntaxValueBenchmarkShape.HiddenCast,
        ModernSyntaxValueBenchmarkShape.FoldNullCheck,
        ModernSyntaxValueBenchmarkShape.LocalFunction,
        ModernSyntaxValueBenchmarkShape.NullPattern,
        ModernSyntaxValueBenchmarkShape.UnboundGenericName,
        ModernSyntaxValueBenchmarkShape.ReturnedIncrement,
        ModernSyntaxValueBenchmarkShape.SelfAssignedIncrement)]
    public ModernSyntaxValueBenchmarkShape CurrentShape { get; set; }

    /// <summary>Builds the benchmark document and representative diagnostic.</summary>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _workspace = new AdhocWorkspace();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(
            _workspace,
            ModernSyntaxValueBenchmarkSource.GenerateCodeFix(Nodes, CurrentShape));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _diagnostic = CreateDiagnostic();
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

    /// <summary>Benchmarks applying one modern-syntax value code fix.</summary>
    /// <returns>The updated document text length.</returns>
    [Benchmark]
    public async Task<int> ModernSyntaxValue_ApplyFixAsync()
    {
        var updated = ModernSyntaxValueCodeFixProvider.Apply(_document, _root, _diagnostic);
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

    /// <summary>Creates the representative diagnostic for the selected shape.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateDiagnostic()
        => CreateOriginalDiagnostic() ?? CreateAdditionalDiagnostic();

    /// <summary>Creates a representative diagnostic for the original value-shape batch.</summary>
    /// <returns>The diagnostic, or <see langword="null"/>.</returns>
    private Diagnostic? CreateOriginalDiagnostic()
        => CurrentShape switch
        {
            ModernSyntaxValueBenchmarkShape.Interpolation => CreateInterpolationDiagnostic(),
            ModernSyntaxValueBenchmarkShape.IgnoredValue => CreateIgnoredValueDiagnostic(),
            ModernSyntaxValueBenchmarkShape.OverwrittenValue => CreateOverwrittenValueDiagnostic(),
            ModernSyntaxValueBenchmarkShape.CoalesceAssignment => CreateCoalesceAssignmentDiagnostic(),
            ModernSyntaxValueBenchmarkShape.AnonymousTuple => CreateAnonymousTupleDiagnostic(),
            ModernSyntaxValueBenchmarkShape.ForeachCast => CreateForeachCastDiagnostic(),
            ModernSyntaxValueBenchmarkShape.HiddenCast => CreateHiddenCastDiagnostic(),
            ModernSyntaxValueBenchmarkShape.FoldNullCheck => CreateFoldNullCheckDiagnostic(),
            _ => null
        };

    /// <summary>Creates a representative diagnostic for the additional value-shape batch.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateAdditionalDiagnostic()
        => CurrentShape switch
        {
            ModernSyntaxValueBenchmarkShape.LocalFunction => CreateLocalFunctionDiagnostic(),
            ModernSyntaxValueBenchmarkShape.NullPattern => CreateNullPatternDiagnostic(),
            ModernSyntaxValueBenchmarkShape.ReturnedIncrement => CreateReturnedIncrementDiagnostic(),
            ModernSyntaxValueBenchmarkShape.SelfAssignedIncrement => CreateSelfAssignedIncrementDiagnostic(),
            _ => CreateUnboundGenericNameDiagnostic()
        };

    /// <summary>Creates an interpolation diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateInterpolationDiagnostic()
    {
        var interpolation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InterpolationSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is InvocationExpressionSyntax);
        return Diagnostic.Create(ModernSyntaxRules.SimplifyInterpolation, interpolation.Expression.GetLocation());
    }

    /// <summary>Creates an ignored-value diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateIgnoredValueDiagnostic()
    {
        var statement = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<ExpressionStatementSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is InvocationExpressionSyntax);
        return Diagnostic.Create(ModernSyntaxRules.MakeIgnoredExpressionValueExplicit, statement.Expression.GetLocation());
    }

    /// <summary>Creates an overwritten-value diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateOverwrittenValueDiagnostic()
    {
        var initializer = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<EqualsValueClauseSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Value is LiteralExpressionSyntax);
        return Diagnostic.Create(ModernSyntaxRules.RemoveOverwrittenValue, initializer.Value.GetLocation());
    }

    /// <summary>Creates a coalescing-assignment diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateCoalesceAssignmentDiagnostic()
    {
        var ifStatement = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<IfStatementSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Condition is IsPatternExpressionSyntax);
        return Diagnostic.Create(ModernSyntaxRules.UseCoalesceAssignment, ifStatement.IfKeyword.GetLocation());
    }

    /// <summary>Creates an anonymous tuple diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateAnonymousTupleDiagnostic()
    {
        var anonymous = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<AnonymousObjectCreationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static _ => true);
        return Diagnostic.Create(ModernSyntaxRules.ConvertAnonymousObjectToTuple, anonymous.NewKeyword.GetLocation());
    }

    /// <summary>Creates a foreach-cast diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateForeachCastDiagnostic()
    {
        var foreachStatement = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<ForEachStatementSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static _ => true);
        var properties = ImmutableDictionary<string, string?>.Empty.Add(ModernSyntaxValueAnalyzer.ElementTypeProperty, "string");
        return Diagnostic.Create(ModernSyntaxRules.AddExplicitForeachCast, foreachStatement.ForEachKeyword.GetLocation(), properties);
    }

    /// <summary>Creates a hidden-cast diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateHiddenCastDiagnostic()
    {
        var cast = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<CastExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Type is IdentifierNameSyntax { Identifier.ValueText: "Derived" });
        var properties = ImmutableDictionary<string, string?>.Empty.Add(ModernSyntaxValueAnalyzer.TypeProperty, "Base");
        return Diagnostic.Create(ModernSyntaxRules.AddVisibleInnerCast, cast.GetLocation(), properties);
    }

    /// <summary>Creates a null-check fold diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateFoldNullCheckDiagnostic()
    {
        var ifStatement = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<IfStatementSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Condition is BinaryExpressionSyntax);
        var properties = ImmutableDictionary<string, string?>.Empty.Add(ModernSyntaxValueAnalyzer.FoldKindProperty, ModernSyntaxValueAnalyzer.ThrowFold);
        return Diagnostic.Create(ModernSyntaxRules.FoldNullCheckIntoAssignment, ifStatement.IfKeyword.GetLocation(), properties);
    }

    /// <summary>Creates a local-function diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateLocalFunctionDiagnostic()
    {
        var variable = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<VariableDeclaratorSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Initializer?.Value is LambdaExpressionSyntax);
        return Diagnostic.Create(ModernSyntaxRules.UseLocalFunction, variable.Identifier.GetLocation());
    }

    /// <summary>Creates a direct-null-pattern diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateNullPatternDiagnostic()
    {
        var pattern = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<DeclarationPatternSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Type is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword });
        return Diagnostic.Create(ModernSyntaxRules.UseDirectNullPattern, pattern.GetLocation());
    }

    /// <summary>Creates a return-discarded step diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateReturnedIncrementDiagnostic()
    {
        var postfix = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<PostfixUnaryExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static _ => true);
        return Diagnostic.Create(ModernSyntaxRules.RemoveOverwrittenValue, postfix.GetLocation());
    }

    /// <summary>Creates a self-assigned step diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateSelfAssignedIncrementDiagnostic()
    {
        var assignment = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<AssignmentExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Right is PostfixUnaryExpressionSyntax);
        return Diagnostic.Create(ModernSyntaxRules.RemoveOverwrittenValue, assignment.GetLocation());
    }

    /// <summary>Creates a generic nameof diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    private Diagnostic CreateUnboundGenericNameDiagnostic()
    {
        var invocation = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<InvocationExpressionSyntax>(
            _root,
            Nodes / MiddleNodeDivisor,
            static node => node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" });
        return Diagnostic.Create(ModernSyntaxRules.UseUnboundGenericName, invocation.Expression.GetLocation());
    }
}
