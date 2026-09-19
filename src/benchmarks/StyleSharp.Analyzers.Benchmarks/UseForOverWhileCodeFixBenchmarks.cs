// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Profiles while-loop code-fix registration and application.</summary>
[System.Diagnostics.DebuggerDisplay("UseForOverWhileCodeFixBenchmarks: {Nodes}")]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class UseForOverWhileCodeFixBenchmarks : IDisposable
{
    /// <summary>The composition container that owns the provider.</summary>
    private CompositionHost _container = null!;

    /// <summary>The exported provider.</summary>
    private CodeFixProvider _provider = null!;

    /// <summary>The valid counted loop.</summary>
    private StructuralCodeFixBenchmarkContext<WhileStatementSyntax> _valid = null!;

    /// <summary>The loop with an extra counter assignment.</summary>
    private StructuralCodeFixBenchmarkContext<WhileStatementSyntax> _reassigned = null!;

    /// <summary>The registration context for the valid loop.</summary>
    private CodeFixContext _validRegistration;

    /// <summary>The registration context for the reassigned counter.</summary>
    private CodeFixContext _reassignedRegistration;

    /// <summary>The action offered by the provider.</summary>
    private CodeAction? _action;

    /// <summary>The number of actions offered during registration.</summary>
    private int _actionCount;

    /// <summary>Whether the owned resources have been released.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the number of input types.</summary>
    [Params(BenchmarkParameterValues.SmallNodeCount, BenchmarkParameterValues.LargeNodeCount)]
    public int Nodes { get; set; }

    /// <summary>Prepares both loop shapes and checks whether a fix is offered.</summary>
    /// <returns>A task representing setup.</returns>
    /// <exception cref="InvalidOperationException">The provider offers a fix for the wrong loop shape.</exception>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _container = new ContainerConfiguration().WithPart<Sst2287UseForOverWhileCodeFixProvider>().CreateContainer();
        _provider = _container.GetExport<CodeFixProvider>();
        _valid = await CreateAsync(Nodes, violating: true).ConfigureAwait(false);
        _reassigned = await CreateAsync(Nodes, violating: false).ConfigureAwait(false);
        _validRegistration = CreateRegistration(_valid);
        _reassignedRegistration = CreateRegistration(_reassigned);
        if (await RegisterAsync().ConfigureAwait(false) != 1
            || await RegisterReassignedAsync().ConfigureAwait(false) != 0)
        {
            throw new InvalidOperationException("Expected a fix only for the loop without another counter assignment.");
        }
    }

    /// <summary>Registers a fix against a cached document.</summary>
    /// <returns>The number of offered fixes.</returns>
    [Benchmark]
    public async Task<int> RegisterAsync()
    {
        _action = null;
        _actionCount = 0;
        await _provider.RegisterCodeFixesAsync(_validRegistration).ConfigureAwait(false);
        return _actionCount;
    }

    /// <summary>Checks a stale diagnostic after the loop gains a counter assignment.</summary>
    /// <returns>The number of offered fixes, which must be zero.</returns>
    [Benchmark]
    public async Task<int> RegisterReassignedAsync()
    {
        _action = null;
        _actionCount = 0;
        await _provider.RegisterCodeFixesAsync(_reassignedRegistration).ConfigureAwait(false);
        return _actionCount;
    }

    /// <summary>Creates and resolves a fresh code action.</summary>
    /// <returns>The number of operations produced by the action.</returns>
    [Benchmark]
    public async Task<int> ApplyAsync()
    {
        _ = await RegisterAsync().ConfigureAwait(false);
        return (await _action!.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false)).Length;
    }

    /// <summary>Releases the workspaces and provider container.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Releases the workspaces and provider container.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases owned resources once.</summary>
    /// <param name="disposing">Whether managed resources can be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _valid?.Dispose();
        _reassigned?.Dispose();
        _container?.Dispose();
        _disposed = true;
    }

    /// <summary>Creates a document with one representative loop.</summary>
    /// <param name="nodes">The number of input types.</param>
    /// <param name="violating">Whether the loop permits conversion.</param>
    /// <returns>The prepared document and loop.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<StructuralCodeFixBenchmarkContext<WhileStatementSyntax>> CreateAsync(int nodes, bool violating) =>
        StructuralCodeFixBenchmarkHelper.CreateAsync(
            nodes,
            count => UseForOverWhileBenchmarkSource.Generate(count, violating),
            static (root, index) => CodeFixBenchmarkSyntaxLookup.GetNthDescendant<WhileStatementSyntax>(root, index, static _ => true));

    /// <summary>Creates a diagnostic and callback for a representative loop.</summary>
    /// <param name="context">The prepared document and loop.</param>
    /// <returns>The registration inputs.</returns>
    private CodeFixContext CreateRegistration(StructuralCodeFixBenchmarkContext<WhileStatementSyntax> context) =>
        new(context.Document, Diagnostic.Create(ModernSyntaxRules.UseForOverWhile, context.Node.WhileKeyword.GetLocation(), "i"), RecordAction, CancellationToken.None);

    /// <summary>Retains the registered action.</summary>
    /// <param name="action">The offered code action.</param>
    /// <param name="diagnostics">The diagnostics addressed by the action.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordAction(CodeAction action, ImmutableArray<Diagnostic> diagnostics)
    {
        _action = action;
        _actionCount++;
    }
}
