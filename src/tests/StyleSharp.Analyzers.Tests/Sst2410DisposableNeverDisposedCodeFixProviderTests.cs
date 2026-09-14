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

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests disposal fixes for stale declarations and nested asynchronous scopes.</summary>
public class Sst2410DisposableNeverDisposedCodeFixProviderTests
{
    /// <summary>Checks declaration shape and interface availability control both code-fix entry points.</summary>
    /// <param name="source">The current document.</param>
    /// <param name="target">The stale diagnostic text.</param>
    /// <param name="includeReferences">Whether disposal interfaces are available.</param>
    /// <param name="canRewrite">Whether the declaration supports a synchronous using declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { return; } }", "return", true, false)]
    [Arguments("class C { object value = new object(); }", "value = new object()", true, false)]
    [Arguments("class C { void M() { using var value = new System.IO.MemoryStream(); } }", "value = new System.IO.MemoryStream()", true, false)]
    [Arguments("class C { void M() { const int value = 1; } }", "value = 1", true, false)]
    [Arguments("class C { void M() { System.IO.MemoryStream value = new(), other = new(); } }", "value = new()", true, false)]
    [Arguments("class C { void M() { System.IDisposable value; } }", "value", true, false)]
    [Arguments("class C { void M() { var value = null; } }", "value = null", true, false)]
    [Arguments("class C { void M() { var value = new object(); } }", "value = new object()", true, false)]
    [Arguments("class C { void M() { var value = new C(); } }", "value = new C()", false, false)]
    [Arguments("class C { object P { get { var value = new System.IO.MemoryStream(); return null; } } }", "value = new System.IO.MemoryStream()", true, true)]
    [Arguments("var value = new System.IO.MemoryStream();", "value = new System.IO.MemoryStream()", true, true)]
    public async Task DeclarationShapeControlsRegistrationAndBatchEditsAsync(string source, string target, bool includeReferences, bool canRewrite)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("DisposableShape", LanguageNames.CSharp);
        if (includeReferences)
        {
            project = project.WithMetadataReferences(RuntimeMetadataReferences.Platform);
        }

        var document = project.AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2410", target);
        using var container = new ContainerConfiguration().WithPart<Sst2410DisposableNeverDisposedCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2410DisposableNeverDisposedCodeFixProvider>(editor, diagnostic);
        if (canRewrite)
        {
            await Assert.That(actions.Count).IsEqualTo(1);
            await Assert.That(editor.GetChangedRoot().DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Single().UsingKeyword.IsKind(SyntaxKind.UsingKeyword)).IsTrue();
        }
        else
        {
            await Assert.That(actions).IsEmpty();
            await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
        }
    }

    /// <summary>Checks the nearest callable scope determines whether asynchronous disposal is valid.</summary>
    /// <param name="body">The method or accessor containing the local.</param>
    /// <param name="canAwait">Whether the local's own scope is asynchronous.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(
        "async System.Threading.Tasks.Task M() { System.Func<System.Threading.Tasks.Task> f = async () => { var value = new Resource(); await System.Threading.Tasks.Task.Yield(); }; await f(); }",
        true)]
    [Arguments("async System.Threading.Tasks.Task M() { System.Action f = () => { var value = new Resource(); }; await System.Threading.Tasks.Task.Yield(); }", false)]
    [Arguments("void M() { async System.Threading.Tasks.Task Local() { var value = new Resource(); await System.Threading.Tasks.Task.Yield(); } }", true)]
    [Arguments("async System.Threading.Tasks.Task M() { void Local() { var value = new Resource(); } await System.Threading.Tasks.Task.Yield(); }", false)]
    [Arguments("object P { get { var value = new Resource(); return null; } }", false)]
    public async Task NearestScopeControlsAwaitUsingAsync(string body, bool canAwait)
    {
        using var workspace = new AdhocWorkspace();
        var source = $"class Resource : System.IAsyncDisposable {{ public System.Threading.Tasks.ValueTask DisposeAsync() => default; }} class C {{ {body} }}";
        var document = workspace.AddProject("DisposableScope", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2410", "value = new Resource()");
        using var container = new ContainerConfiguration().WithPart<Sst2410DisposableNeverDisposedCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(canAwait ? 1 : 0);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2410DisposableNeverDisposedCodeFixProvider>(editor, diagnostic);
        var changedRoot = editor.GetChangedRoot();
        if (canAwait)
        {
            var declaration = changedRoot.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
                .Single(static statement => statement.Declaration.Variables[0].Identifier.ValueText == "value");
            await Assert.That(declaration.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword)).IsTrue();
            await Assert.That(declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)).IsTrue();
            var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
            var changedDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
            await Assert.That(SyntaxFactory.AreEquivalent((await changedDocument.GetSyntaxRootAsync())!, changedRoot, ignoreChildNode: static _ => false)).IsTrue();
        }
        else
        {
            await Assert.That(changedRoot.ToFullString()).IsEqualTo(source);
        }
    }
}
