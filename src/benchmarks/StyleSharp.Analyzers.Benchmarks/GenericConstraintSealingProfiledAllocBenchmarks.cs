// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Profiles registration and application of sealing decisions involving generic constraints.</summary>
[System.Diagnostics.DebuggerDisplay("GenericConstraintSealingProfiledAllocBenchmarks: {Scenario}")]
[MemoryDiagnoser]
[ShortRunJob]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class GenericConstraintSealingProfiledAllocBenchmarks : IDisposable
{
    /// <summary>The class targeted by every benchmark scenario.</summary>
    private const string TargetTypeName = "Runner";

    /// <summary>The composition container that owns the exported provider.</summary>
    private readonly CompositionHost _container = new ContainerConfiguration()
        .WithPart<Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider>().CreateContainer();

    /// <summary>The workspace that owns the input document.</summary>
    private readonly AdhocWorkspace _workspace = new();

    /// <summary>The exported code-fix provider.</summary>
    private CodeFixProvider _provider = null!;

    /// <summary>The document reused for each operation.</summary>
    private Document _document = null!;

    /// <summary>The input syntax root.</summary>
    private SyntaxNode _root = null!;

    /// <summary>The abstract declaration targeted by the diagnostic.</summary>
    private ClassDeclarationSyntax _declaration = null!;

    /// <summary>The registration inputs and callback allocated during setup.</summary>
    private CodeFixContext _registrationContext;

    /// <summary>The action registered by the provider.</summary>
    private CodeAction? _action;

    /// <summary>The number of actions registered by one invocation.</summary>
    private int _actionCount;

    /// <summary>Whether this scenario permits a sealed class.</summary>
    private bool _seal;

    /// <summary>Whether the owned resources have been released.</summary>
    private bool _disposed;

    /// <summary>Gets or sets the kind of generic constraint or inheritance contract.</summary>
    [Params("Ordinary", "SelfConstraint", "MethodConstraint", "ExternalConstraint", "GenericArgument", "ProtectedMember")]
    public string Scenario { get; set; } = string.Empty;

    /// <summary>Prepares registration and checks the offered action against compiler diagnostics.</summary>
    /// <returns>A task representing validation of the code-fix action.</returns>
    /// <exception cref="InvalidOperationException">The action is missing, chooses an invalid modifier, or produces compilation errors.</exception>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _provider = _container.GetExport<CodeFixProvider>();
        _document = CodeFixBenchmarkDocumentFactory.CreateDocument(_workspace, GenericConstraintSealingBenchmarkSource.Generate(Scenario));
        _root = (await _document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        _declaration = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<ClassDeclarationSyntax>(_root, 0, static candidate => candidate.Identifier.ValueText == TargetTypeName);
        _seal = Scenario is "Ordinary" or "GenericArgument";
        var diagnostic = Diagnostic.Create(MaintainabilityRules.AbstractTypeWithoutAbstractMembers, _declaration.Identifier.GetLocation(), TargetTypeName);
        _registrationContext = new(_document, diagnostic, RecordAction, CancellationToken.None);
        var count = await RegisterAsync().ConfigureAwait(false);
        if (count != 1 || _action is null)
        {
            throw new InvalidOperationException($"Expected one code action for {Scenario}, received {count}.");
        }

        var operations = await _action.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (var operation in operations)
        {
            if (operation is not ApplyChangesOperation changes)
            {
                continue;
            }

            await ValidateAsync(changes.ChangedSolution.GetDocument(_document.Id)!).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("The code action did not provide a changed document.");
    }

    /// <summary>Measures the sealing decision and action registration.</summary>
    /// <returns>The number of registered actions.</returns>
    [Benchmark]
    public async Task<int> RegisterAsync()
    {
        _action = null;
        _actionCount = 0;
        await _provider.RegisterCodeFixesAsync(_registrationContext).ConfigureAwait(false);
        return _actionCount;
    }

    /// <summary>Measures the selected syntax edit after its sealing decision has been validated.</summary>
    /// <returns>The edited document's text length.</returns>
    [Benchmark]
    public async Task<int> ApplyAsync()
    {
        var updated = Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider.Apply(_document, _root, _declaration, _seal);
        return (await updated.GetTextAsync().ConfigureAwait(false)).Length;
    }

    /// <summary>Releases the benchmark's workspace and composition container.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Releases the benchmark's workspace and composition container.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the owned resources once.</summary>
    /// <param name="disposing">Whether managed resources can be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _container.Dispose();
        _workspace.Dispose();
        _disposed = true;
    }

    /// <summary>Retains the registered action for setup validation and counts registrations.</summary>
    /// <param name="action">The code action offered by the provider.</param>
    /// <param name="diagnostics">The diagnostics addressed by the action.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordAction(CodeAction action, ImmutableArray<Diagnostic> diagnostics)
    {
        _action = action;
        _actionCount++;
    }

    /// <summary>Verifies that the chosen modifier is correct and all generic constraints compile.</summary>
    /// <param name="document">The document produced by the offered action.</param>
    /// <returns>A task representing compiler validation.</returns>
    /// <exception cref="InvalidOperationException">The action chooses an invalid modifier or causes a compiler error.</exception>
    private async Task ValidateAsync(Document document)
    {
        var root = (await document.GetSyntaxRootAsync().ConfigureAwait(false))!;
        var declaration = CodeFixBenchmarkSyntaxLookup.GetNthDescendant<ClassDeclarationSyntax>(root, 0, static candidate => candidate.Identifier.ValueText == TargetTypeName);
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) || declaration.Modifiers.Any(SyntaxKind.SealedKeyword) != _seal)
        {
            throw new InvalidOperationException($"The action chose an invalid class modifier for {Scenario}.");
        }

        var compilation = (await document.Project.GetCompilationAsync().ConfigureAwait(false))!;
        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                throw new InvalidOperationException(diagnostic.ToString());
            }
        }
    }
}
