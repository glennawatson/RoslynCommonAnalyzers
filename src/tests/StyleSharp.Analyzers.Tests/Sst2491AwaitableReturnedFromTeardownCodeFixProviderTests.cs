// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests teardown fixes for owned returns, local functions, and stale diagnostics.</summary>
public class Sst2491AwaitableReturnedFromTeardownCodeFixProviderTests
{
    /// <summary>The document name shared by the teardown syntax tests.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>A teardown return sharing a method with returns owned by nested functions.</summary>
    private const string NestedReturnsSource = """
        class C
        {
            Task M(Task pending)
            {
                try { return pending; } finally { }
                Task Local() { return pending; }
                System.Func<Task> lambda = () => { return pending; };
                System.Func<Task> anonymous = delegate { return pending; };
            }
        }
        """;

    /// <summary>The owning method becomes async while every nested function stays unchanged.</summary>
    private const string NestedReturnsExpected = """
        class C
        {
            async Task M(Task pending)
            {
                try { { await pending; return; } } finally { }
                Task Local() { return pending; }
                System.Func<Task> lambda = () => { return pending; };
                System.Func<Task> anonymous = delegate { return pending; };
            }
        }
        """;

    /// <summary>Verifies single and batch fixes await each owned return and preserve nested functions.</summary>
    /// <param name="source">The method or local function before rewriting.</param>
    /// <param name="expected">The complete syntax expected after rewriting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(
        "class C { Task<int> M(Task<int> pending) { try { return pending; } finally { } } }",
        "class C { async Task<int> M(Task<int> pending) { try { return await pending; } finally { } } }")]
    [Arguments(
        "class C { public Task M(Task pending) { try { return pending; } finally { } } }",
        "class C { public async Task M(Task pending) { try { { await pending; return; } } finally { } } }")]
    [Arguments(
        "class C { ValueTask M(ValueTask pending) { try { return pending; } finally { } } }",
        "class C { async ValueTask M(ValueTask pending) { try { { await pending; return; } } finally { } } }")]
    [Arguments(
        "class C { ValueTask<int> M(ValueTask<int> pending) { try { return pending; } finally { } } }",
        "class C { async ValueTask<int> M(ValueTask<int> pending) { try { return await pending; } finally { } } }")]
    [Arguments(
        "class C { void M(Task<int> pending) { Task<int> Local() { try { return pending; } finally { } } } }",
        "class C { void M(Task<int> pending) { async Task<int> Local() { try { return await pending; } finally { } } } }")]
    [Arguments(
        "class C { void M() { static Task Local(Task pending) { try { return pending; } finally { } } } }",
        "class C { void M() { static async Task Local(Task pending) { try { { await pending; return; } } finally { } } } }")]
    [Arguments(
        "class C { void M(Task<int> pending) { lock (this) { Task<int> Local() { try { return pending; } finally { } } } } }",
        "class C { void M(Task<int> pending) { lock (this) { async Task<int> Local() { try { return await pending; } finally { } } } } }")]
    [Arguments(
        "class C { Task<int> M(Task<int> pending, bool first) { try { if (first) return pending; return pending; } finally { } } }",
        "class C { async Task<int> M(Task<int> pending, bool first) { try { if (first) return await pending; return await pending; } finally { } } }")]
    [Arguments(NestedReturnsSource, NestedReturnsExpected)]
    [Arguments(
        "class C { // method\n Task<int> M(Task<int> pending) { try { return /* pending */ pending; } finally { } } }",
        "class C { // method\n async Task<int> M(Task<int> pending) { try { return /* pending */ await pending; } finally { } } }")]
    public async Task OwnedReturnsAreAwaitedAsync(string source, string expected)
    {
        const string Imports = "using System.Threading.Tasks; ";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument(DocumentName, SourceText.From(Imports + source));
        var root = (await document.GetSyntaxRootAsync())!;
        var statement = root.DescendantNodes().OfType<ReturnStatementSyntax>().First();
        var function = statement.Ancestors().First(static node => node is MethodDeclarationSyntax or LocalFunctionStatementSyntax);
        var diagnostic = Diagnostic.Create(CorrectnessRules.AwaitableReturnedFromTeardown, statement.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2491AwaitableReturnedFromTeardownCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST2491");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(BatchEditFixAllProvider.Instance);
        await Assert.That(((IBatchEditKeyProvider)provider).TryGetBatchEditSpan(root, diagnostic, out var span)).IsTrue();
        await Assert.That(span).IsEqualTo(function.Span);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(Imports + expected).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
    }

    /// <summary>Verifies stale diagnostics cannot rewrite unsupported functions or return types.</summary>
    /// <param name="source">The unsupported return or stale diagnostic location.</param>
    /// <param name="hasFunctionSpan">Whether the location still belongs to a method or local function.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }", false)]
    [Arguments("class C { int M() => 1; }", false)]
    [Arguments("return 1;", false)]
    [Arguments("{ return 1; }", false)]
    [Arguments("class C { C() { return; } }", false)]
    [Arguments("class C { int P { get { return 1; } } }", false)]
    [Arguments("class C { System.Func<int> value = () => { return 1; }; }", false)]
    [Arguments("class C { System.Func<int> value = delegate { return 1; }; }", false)]
    [Arguments("class C { async System.Threading.Tasks.Task<int> M() { return 1; } }", true)]
    [Arguments("class C { void M() { async System.Threading.Tasks.Task<int> Local() { return 1; } } }", true)]
    [Arguments("class C { void M() { return; } }", true)]
    [Arguments("class C { void M() { return; void Local() { return; } System.Action action = () => { return; }; } }", true)]
    [Arguments("class C { System.Threading.Tasks.Task M(System.Threading.Tasks.Task pending) { lock (this) { return pending; } } }", true)]
    [Arguments(
        "class C { System.Threading.Tasks.Task M(System.Threading.Tasks.Task pending, bool first) { if (first) { try { return pending; } finally { } } lock (this) { return pending; } } }",
        true)]
    [Arguments("class C { T M<T>(T value) { return value; } }", true)]
    [Arguments("class C { int[] M(int[] value) { return value; } }", true)]
    [Arguments("class C { dynamic M(dynamic value) { return value; } }", true)]
    [Arguments("class C { unsafe int* M(int* value) { return value; } }", true)]
    public async Task UnsupportedReturnHasNoFixAsync(string source, bool hasFunctionSpan)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument(DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var statement = root.DescendantNodes().OfType<ReturnStatementSyntax>().FirstOrDefault();
        var diagnostic = Diagnostic.Create(CorrectnessRules.AwaitableReturnedFromTeardown, statement?.GetLocation() ?? root.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2491AwaitableReturnedFromTeardownCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(((IBatchEditKeyProvider)provider).TryGetBatchEditSpan(root, diagnostic, out var span)).IsEqualTo(hasFunctionSpan);
        var expectedSpan = hasFunctionSpan
            ? statement!.Ancestors().First(static node => node is MethodDeclarationSyntax or LocalFunctionStatementSyntax).Span
            : default;
        await Assert.That(span).IsEqualTo(expectedSpan);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
