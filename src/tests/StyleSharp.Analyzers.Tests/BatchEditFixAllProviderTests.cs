// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests duplicate filtering and recovery from overlapping syntax edits.</summary>
public class BatchEditFixAllProviderTests
{
    /// <summary>The number of distinct statement targets in the fixture.</summary>
    private const int DistinctStatementCount = 2;

    /// <summary>The document name used by the workspace fixtures.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The source with two removable statements.</summary>
    private const string RemovalSource = "class C { void M() { A(); B(); } }";

    /// <summary>The equivalence key identifying statement removal.</summary>
    private const string RemovalKey = "remove";

    /// <summary>The unchanged source used when no batch edit applies.</summary>
    private const string EmptyClassSource = "class C { }";

    /// <summary>The diagnostic used to request statement removal.</summary>
    private static readonly DiagnosticDescriptor Rule = new("TEST001", "Remove", "Remove", "Testing", DiagnosticSeverity.Warning, true);

    /// <summary>A distinct diagnostic that can select the same statement.</summary>
    private static readonly DiagnosticDescriptor OtherRule = new("TEST002", "Other", "Other", "Testing", DiagnosticSeverity.Warning, true);

    /// <summary>Verifies collection and iterator filtering agree when targets coincide or keys are unavailable.</summary>
    /// <param name="resolveKey">Whether the fix resolves diagnostics to a shared edit span.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ResolvedTargetsAreDeduplicatedAsync(bool resolveKey)
    {
        var root = await CSharpSyntaxTree.ParseText(RemovalSource).GetRootAsync();
        var statements = root.DescendantNodes().OfType<ExpressionStatementSyntax>().ToArray();
        var first = Diagnostic.Create(Rule, statements[0].GetLocation());
        var second = Diagnostic.Create(Rule, statements[1].GetLocation());
        ImmutableArray<Diagnostic> diagnostics = [first, first, second];
        var fix = new KeyedRemovalFix(resolveKey);
        var collected = BatchEditFixAllProvider.CollectUniqueDiagnostics(root, fix, diagnostics);
        var enumerated = BatchEditFixAllProvider.UniqueDiagnostics(root, fix, diagnostics).ToArray();
        await Assert.That(collected.Count).IsEqualTo(resolveKey ? 1 : DistinctStatementCount);
        await Assert.That(enumerated).IsEquivalentTo(collected);
        await Assert.That(collected[0]).IsSameReferenceAs(first);
        await Assert.That(BatchEditFixAllProvider.CollectUniqueDiagnostics(root, new RemovalFix(), diagnostics).Count).IsEqualTo(DistinctStatementCount);
        await Assert.That(BatchEditFixAllProvider.CollectUniqueDiagnostics(root, fix, []).Count).IsEqualTo(0);
    }

    /// <summary>Verifies conflicting removals retain all compatible edits through the recovery path.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OverlappingRemovalsKeepCompatibleEditsAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, RemovalSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var statements = root.DescendantNodes().OfType<ExpressionStatementSyntax>().ToArray();
        ImmutableArray<Diagnostic> diagnostics =
        [
            Diagnostic.Create(Rule, statements[0].GetLocation()),
            Diagnostic.Create(Rule, statements[0].Expression.GetLocation()),
            Diagnostic.Create(Rule, statements[1].GetLocation()),
        ];
        var fix = new RemovalFix();
        var context = new FixAllContext(document, fix, FixAllScope.Document, RemovalKey, fix.FixableDiagnosticIds, new SelectedDiagnostics(diagnostics), CancellationToken.None);
        var action = (await BatchEditFixAllProvider.Instance.GetFixAsync(context))!;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetSyntaxRootAsync())!.DescendantNodes().OfType<ExpressionStatementSyntax>()).IsEmpty();
    }

    /// <summary>Verifies unrelated registration errors are propagated rather than treated as duplicate edits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnrelatedRegistrationFailureIsPropagatedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, EmptyClassSource);
        var editor = await DocumentEditor.CreateAsync(document);
        var diagnostic = Diagnostic.Create(Rule, editor.OriginalRoot.GetLocation());
        await Assert.That(() => BatchEditFixAllProvider.RegisterBatchEdit(editor, new FailingFix(), diagnostic)).Throws<InvalidOperationException>();
    }

    /// <summary>Verifies Fix All resolves edit keys before removing duplicate targets.</summary>
    /// <param name="resolveKey">Whether both diagnostics resolve to a shared target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task FixAllDeduplicatesResolvedTargetsAsync(bool resolveKey)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, RemovalSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var statements = root.DescendantNodes().OfType<ExpressionStatementSyntax>().ToArray();
        var first = Diagnostic.Create(Rule, statements[0].GetLocation());
        var second = Diagnostic.Create(Rule, statements[1].GetLocation());
        var fix = new KeyedRemovalFix(resolveKey);
        var context = new FixAllContext(document, fix, FixAllScope.Document, RemovalKey, fix.FixableDiagnosticIds, new SelectedDiagnostics([first, first, second]), CancellationToken.None);
        var action = (await BatchEditFixAllProvider.Instance.GetFixAsync(context))!;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetSyntaxRootAsync())!.DescendantNodes().OfType<ExpressionStatementSyntax>().Count()).IsEqualTo(resolveKey ? 1 : 0);
    }

    /// <summary>Verifies a provider without batch support leaves the document unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ProviderWithoutBatchSupportIsIgnoredAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, EmptyClassSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(Rule, root.GetLocation());
        var fix = new NonBatchFix();
        var context = new FixAllContext(document, fix, FixAllScope.Document, RemovalKey, fix.FixableDiagnosticIds, new SelectedDiagnostics([diagnostic]), CancellationToken.None);
        var action = (await BatchEditFixAllProvider.Instance.GetFixAsync(context))!;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution.GetDocument(document.Id) ?? document;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(EmptyClassSource);
    }

    /// <summary>Verifies exact duplicates coalesce while different diagnostic IDs keep their identity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FixAllWithoutKeysHandlesDuplicateSpansAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, RemovalSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var statements = root.DescendantNodes().OfType<ExpressionStatementSyntax>().ToArray();
        var first = Diagnostic.Create(Rule, statements[0].GetLocation());
        var other = Diagnostic.Create(OtherRule, statements[0].GetLocation());
        var second = Diagnostic.Create(Rule, statements[1].GetLocation());
        var fix = new RemovalFix();
        var context = new FixAllContext(
            document,
            fix,
            FixAllScope.Document,
            RemovalKey,
            fix.FixableDiagnosticIds,
            new SelectedDiagnostics([first, first, other, second]),
            CancellationToken.None);
        var action = (await BatchEditFixAllProvider.Instance.GetFixAsync(context))!;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetSyntaxRootAsync())!.DescendantNodes().OfType<ExpressionStatementSyntax>()).IsEmpty();
    }

    /// <summary>Removes the statement selected by a diagnostic.</summary>
    private class RemovalFix : CodeFixProvider, IBatchFixableCodeFix
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds => [Rule.Id, OtherRule.Id];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task RegisterCodeFixesAsync(CodeFixContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

        /// <inheritdoc/>
        public void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        {
            var statement = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ExpressionStatementSyntax>()!;
            editor.RemoveNode(statement);
        }
    }

    /// <summary>Optionally maps all diagnostics to one logical edit target.</summary>
    /// <param name="resolveKey">Whether to provide a shared target.</param>
    private sealed class KeyedRemovalFix(bool resolveKey) : RemovalFix, IBatchEditKeyProvider
    {
        /// <inheritdoc/>
        public bool TryGetBatchEditSpan(SyntaxNode root, Diagnostic diagnostic, out TextSpan span)
        {
            span = root.Span;
            return resolveKey;
        }
    }

    /// <summary>Models a registration failure unrelated to stale syntax.</summary>
    private sealed class FailingFix : IBatchFixableCodeFix
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) => throw new InvalidOperationException("Registration failed");
    }

    /// <summary>Models a code fix that does not support batched edits.</summary>
    private sealed class NonBatchFix : CodeFixProvider
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds => [Rule.Id];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task RegisterCodeFixesAsync(CodeFixContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;
    }

    /// <summary>Supplies the selected diagnostics to Fix All.</summary>
    /// <param name="diagnostics">The diagnostics for the test document.</param>
    private sealed class SelectedDiagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) => Task.FromResult(Enumerable.Empty<Diagnostic>());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
