// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests name simplification edits and rejection of stale diagnostics.</summary>
public class NameSimplificationCodeFixProviderTests
{
    /// <summary>Verifies unsupported syntax produces no registered action or edit.</summary>
    /// <param name="id">The reported diagnostic identifier.</param>
    /// <param name="target">The syntax selected by the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SST1116", "1")]
    [Arguments("SST1116", "value")]
    [Arguments("SST1117", "1")]
    [Arguments("UNKNOWN", "value")]
    public async Task StaleDiagnosticLeavesDocumentUnchangedAsync(string id, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleName", LanguageNames.CSharp).AddDocument("Test.cs", "class C { int value = 1; }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, id, target);
        using var container = new ContainerConfiguration().WithPart<NameSimplificationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(NameSimplificationCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Verifies every supported syntax shape preserves trivia in individual and batch edits.</summary>
    /// <param name="id">The reported diagnostic identifier.</param>
    /// <param name="source">The original source.</param>
    /// <param name="target">The syntax selected by the diagnostic.</param>
    /// <param name="expected">The expected edited source.</param>
    /// <param name="title">The expected action title.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SST1116", "class C { /*keep*/ global::C value; }", "global::C", "class C { /*keep*/ C value; }", "Shorten equivalent name")]
    [Arguments("SST1116", "class C { /*keep*/ System.String value; }", "System.String", "class C { /*keep*/ String value; }", "Shorten equivalent name")]
    [Arguments("SST1117", "class C { int M() => /*keep*/ this.value; }", "this.value", "class C { int M() => /*keep*/ value; }", "Remove this qualification")]
    [Arguments("SST1117", "class C { int M() => /*keep*/ value; }", "value", "class C { int M() => /*keep*/ this.value; }", "Add this qualification")]
    public async Task SupportedNameProducesExpectedEditAsync(string id, string source, string target, string expected, string title)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("NameEdit", LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, id, target);
        using var container = new ContainerConfiguration().WithPart<NameSimplificationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo(title);
        var changed = NameSimplificationCodeFixProvider.Apply(document, root, diagnostic);
        await Assert.That((await changed.GetSyntaxRootAsync())!.ToFullString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }
}
