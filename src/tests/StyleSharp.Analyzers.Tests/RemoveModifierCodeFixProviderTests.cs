// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests modifier removal and trivia preservation for each supported declaration.</summary>
public class RemoveModifierCodeFixProviderTests
{
    /// <summary>The document name used for modifier syntax tests.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies individual and batch fixes preserve the declaration after removing its modifier.</summary>
    /// <param name="source">The declaration containing the modifier.</param>
    /// <param name="expected">The exact syntax after removal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("// type\npartial class C { }", "// type\nclass C { }")]
    [Arguments("// type\npartial struct C { }", "// type\nstruct C { }")]
    [Arguments("// type\npartial interface C { }", "// type\ninterface C { }")]
    [Arguments("// type\npartial record C;", "// type\nrecord C;")]
    [Arguments("// type\npartial record struct C;", "// type\nrecord struct C;")]
    [Arguments("// type\npartial public class C { }", "// type\npublic class C { }")]
    [Arguments("[System.Obsolete] partial class C { }", "[System.Obsolete] class C { }")]
    [Arguments("public partial class C { }", "public class C { }")]
    [Arguments("class C {\n    protected C() { }\n}", "class C {\n    C() { }\n}")]
    [Arguments("class C {\n    protected void M() { }\n}", "class C {\n    void M() { }\n}")]
    [Arguments("class C {\n    protected int P { get; }\n}", "class C {\n    int P { get; }\n}")]
    [Arguments("class C {\n    protected int this[int i] => i;\n}", "class C {\n    int this[int i] => i;\n}")]
    [Arguments("class C {\n    protected event System.Action E { add { } remove { } }\n}", "class C {\n    event System.Action E { add { } remove { } }\n}")]
    [Arguments("class C {\n    protected int value;\n}", "class C {\n    int value;\n}")]
    [Arguments("class C {\n    protected event System.Action E;\n}", "class C {\n    event System.Action E;\n}")]
    [Arguments("class C {\n    protected delegate void D();\n}", "class C {\n    delegate void D();\n}")]
    [Arguments("class C {\n    protected enum E { A }\n}", "class C {\n    enum E { A }\n}")]
    [Arguments("class C {\n    protected ~C() { }\n}", "class C {\n    ~C() { }\n}")]
    public async Task ModifierRemovalPreservesDeclarationAsync(string source, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var token = root.DescendantTokens().First(static token => token.ValueText is "partial" or "protected");
        var diagnostic = Diagnostic.Create(MaintainabilityRules.NoRedundantModifier, token.GetLocation());
        using var container = new ContainerConfiguration().WithPart<RemoveModifierCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);

        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<RemoveModifierCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies stale diagnostics on non-modifier tokens leave the document untouched.</summary>
    /// <param name="source">The source receiving a diagnostic on its first token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System;")]
    [Arguments("class C { void M() { checked { } } }")]
    [Arguments("checked { }")]
    public async Task NonModifierDiagnosticHasNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(MaintainabilityRules.NoRedundantModifier, root.GetFirstToken().GetLocation());
        using var container = new ContainerConfiguration().WithPart<RemoveModifierCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<RemoveModifierCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies a modifier already removed from the declaration is harmless.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingModifierLeavesDeclarationUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From("class C { }"));
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var changed = RemoveModifierCodeFixProvider.RemoveModifier(document, root, declaration, declaration.Keyword);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo("class C { }");
    }
}
