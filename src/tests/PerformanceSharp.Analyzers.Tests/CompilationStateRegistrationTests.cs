// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="CompilationStateRegistration"/>, which hands state built once per compilation to analyzer actions.</summary>
public class CompilationStateRegistrationTests
{
    /// <summary>A class with one field, one method and one property, so each registered kind is seen exactly once.</summary>
    private const string Source = """
        class C
        {
            int _field;
            void M() { }
            int P => _field;
        }
        """;

    /// <summary>The number of separate compilations each probe analyzer runs over.</summary>
    private const int CompilationCount = 2;

    /// <summary>The diagnostic every probe analyzer declares, so the driver does not treat it as suppressed.</summary>
    private static readonly DiagnosticDescriptor ProbeDescriptor = new("TEST0001", "Probe", "Probe", "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <summary>Verifies a syntax node action builds its state once per compilation and receives it for every registered kind.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxNodeActionSharesOneStatePerCompilationAsync()
    {
        var recorder = new Recorder();

        await RunEachCompilationAsync(new SyntaxNodeProbe(recorder));

        await Assert.That(recorder.StatesCreated).IsEqualTo(CompilationCount);
        await AssertEachCompilationSharesOneStateAsync(recorder);
        await Assert.That(recorder.Kinds()).IsEquivalentTo(["Node:MethodDeclaration", "Node:MethodDeclaration", "Node:PropertyDeclaration", "Node:PropertyDeclaration"]);
    }

    /// <summary>Verifies a symbol action builds its state once per compilation and receives it for every registered kind.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SymbolActionSharesOneStatePerCompilationAsync()
    {
        var recorder = new Recorder();

        await RunEachCompilationAsync(new SymbolProbe(recorder));

        await Assert.That(recorder.StatesCreated).IsEqualTo(CompilationCount);
        await AssertEachCompilationSharesOneStateAsync(recorder);
        await Assert.That(recorder.Kinds()).IsEquivalentTo(["Symbol:Field", "Symbol:Field", "Symbol:NamedType", "Symbol:NamedType"]);
    }

    /// <summary>Verifies two syntax node actions share one state per compilation and each runs only for its own kinds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoSyntaxNodeActionsShareOneStatePerCompilationAsync()
    {
        const string first = "First:MethodDeclaration";
        const string second = "Second:PropertyDeclaration";
        var recorder = new Recorder();

        await RunEachCompilationAsync(new TwoSyntaxNodeActionsProbe(recorder));

        await Assert.That(recorder.StatesCreated).IsEqualTo(CompilationCount);
        await AssertEachCompilationSharesOneStateAsync(recorder);
        await Assert.That(recorder.Kinds()).IsEquivalentTo([first, first, second, second]);
    }

    /// <summary>Verifies three syntax node actions share one state per compilation and each runs only for its own kinds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeSyntaxNodeActionsShareOneStatePerCompilationAsync()
    {
        const string first = "First:MethodDeclaration";
        const string second = "Second:PropertyDeclaration";
        const string third = "Third:FieldDeclaration";
        var recorder = new Recorder();

        await RunEachCompilationAsync(new ThreeSyntaxNodeActionsProbe(recorder));

        await Assert.That(recorder.StatesCreated).IsEqualTo(CompilationCount);
        await AssertEachCompilationSharesOneStateAsync(recorder);
        await Assert.That(recorder.Kinds()).IsEquivalentTo([first, first, second, second, third, third]);
    }

    /// <summary>Runs an analyzer over <see cref="CompilationCount"/> separate compilations of <see cref="Source"/>.</summary>
    /// <param name="analyzer">The analyzer to run.</param>
    /// <returns>A task that completes when every run has finished.</returns>
    private static async Task RunEachCompilationAsync(DiagnosticAnalyzer analyzer)
    {
        for (var run = 0; run < CompilationCount; run++)
        {
            var compilation = CSharpCompilation.Create(
                $"Probe{run}",
                [CSharpSyntaxTree.ParseText(Source)],
                [RuntimeMetadataReferences.CoreLibrary],
                new(OutputKind.DynamicallyLinkedLibrary));
            _ = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        }
    }

    /// <summary>Asserts every callback received the state built for its own compilation, and each compilation had exactly one.</summary>
    /// <param name="recorder">The recorder the probe wrote to.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertEachCompilationSharesOneStateAsync(Recorder recorder)
    {
        var calls = recorder.Calls.ToArray();
        await Assert.That(Array.TrueForAll(calls, static call => ReferenceEquals(call.State.Compilation, call.Compilation))).IsTrue();
        await Assert.That(calls.Select(static call => call.State).Distinct().Count()).IsEqualTo(CompilationCount);
        await Assert.That(calls.Select(static call => call.Compilation).Distinct().Count()).IsEqualTo(CompilationCount);
    }

    /// <summary>Records state creation and every callback a probe analyzer receives.</summary>
    private sealed class Recorder
    {
        /// <summary>The number of states the factories have built.</summary>
        private int _statesCreated;

        /// <summary>Gets the number of states the factories have built.</summary>
        public int StatesCreated => Volatile.Read(ref _statesCreated);

        /// <summary>Gets every callback received, in arrival order.</summary>
        public ConcurrentQueue<Call> Calls { get; } = new();

        /// <summary>Builds the state for a starting compilation.</summary>
        /// <param name="compilation">The compilation that is starting.</param>
        /// <returns>The new state.</returns>
        public ProbeState Create(Compilation compilation)
        {
            _ = Interlocked.Increment(ref _statesCreated);
            return new(compilation, this);
        }

        /// <summary>Lists the action and kind of every recorded callback.</summary>
        /// <returns>One <c>Action:Kind</c> entry per callback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string[] Kinds() => Calls.Select(static call => call.Kind).ToArray();
    }

    /// <summary>Registers one syntax node action for methods and properties.</summary>
    /// <param name="recorder">The recorder the state writes to.</param>
    private sealed class SyntaxNodeProbe(Recorder recorder) : DiagnosticAnalyzer
    {
        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ProbeDescriptor];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            CompilationStateRegistration.RegisterSyntaxNodeAction(
                context,
                recorder.Create,
                static (in SyntaxNodeAnalysisContext nodeContext, ProbeState state) => state.RecordNode("Node", nodeContext),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.PropertyDeclaration);
        }
    }

    /// <summary>Registers one symbol action for fields and named types.</summary>
    /// <param name="recorder">The recorder the state writes to.</param>
    private sealed class SymbolProbe(Recorder recorder) : DiagnosticAnalyzer
    {
        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ProbeDescriptor];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            CompilationStateRegistration.RegisterSymbolAction(
                context,
                recorder.Create,
                static (in SymbolAnalysisContext symbolContext, ProbeState state) => state.RecordSymbol(symbolContext),
                SymbolKind.Field,
                SymbolKind.NamedType);
        }
    }

    /// <summary>Registers one syntax node action for methods and a second for properties.</summary>
    /// <param name="recorder">The recorder the state writes to.</param>
    private sealed class TwoSyntaxNodeActionsProbe(Recorder recorder) : DiagnosticAnalyzer
    {
        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ProbeDescriptor];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            CompilationStateRegistration.RegisterSyntaxNodeActions(
                context,
                recorder.Create,
                new(static (in SyntaxNodeAnalysisContext nodeContext, ProbeState state) => state.RecordNode("First", nodeContext), [SyntaxKind.MethodDeclaration]),
                new(static (in SyntaxNodeAnalysisContext nodeContext, ProbeState state) => state.RecordNode("Second", nodeContext), [SyntaxKind.PropertyDeclaration]));
        }
    }

    /// <summary>Registers syntax node actions for methods, properties and fields.</summary>
    /// <param name="recorder">The recorder the state writes to.</param>
    private sealed class ThreeSyntaxNodeActionsProbe(Recorder recorder) : DiagnosticAnalyzer
    {
        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ProbeDescriptor];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            CompilationStateRegistration.RegisterSyntaxNodeActions(
                context,
                recorder.Create,
                new(static (in SyntaxNodeAnalysisContext nodeContext, ProbeState state) => state.RecordNode("First", nodeContext), [SyntaxKind.MethodDeclaration]),
                new(static (in SyntaxNodeAnalysisContext nodeContext, ProbeState state) => state.RecordNode("Second", nodeContext), [SyntaxKind.PropertyDeclaration]),
                new(static (in SyntaxNodeAnalysisContext nodeContext, ProbeState state) => state.RecordNode("Third", nodeContext), [SyntaxKind.FieldDeclaration]));
        }
    }

    /// <summary>One callback received by a probe analyzer.</summary>
    /// <param name="Kind">The action and the syntax or symbol kind it ran for.</param>
    /// <param name="State">The state the callback received.</param>
    /// <param name="Compilation">The compilation the callback ran in.</param>
    private sealed record Call(string Kind, ProbeState State, Compilation Compilation);

    /// <summary>The per-compilation state handed to the probe callbacks.</summary>
    /// <param name="Compilation">The compilation the state was built for.</param>
    /// <param name="Recorder">The recorder the callbacks write to.</param>
    private sealed record ProbeState(Compilation Compilation, Recorder Recorder)
    {
        /// <summary>Records a syntax node callback.</summary>
        /// <param name="action">The name of the action that ran.</param>
        /// <param name="context">The syntax node analysis context.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordNode(string action, in SyntaxNodeAnalysisContext context) =>
            Recorder.Calls.Enqueue(new($"{action}:{context.Node.Kind()}", this, context.Compilation));

        /// <summary>Records a symbol callback.</summary>
        /// <param name="context">The symbol analysis context.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordSymbol(in SymbolAnalysisContext context) =>
            Recorder.Calls.Enqueue(new($"Symbol:{context.Symbol.Kind}", this, context.Compilation));
    }
}
