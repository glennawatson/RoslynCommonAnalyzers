// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests redundant modifier removal across member shapes and repeated batch edits.</summary>
public class Sst1491RedundantModifierCodeFixProviderTests
{
    /// <summary>The redundant modifier removed by these tests.</summary>
    private const string PublicModifier = "public";

    /// <summary>Verifies modifier removal preserves the declaration and its indentation.</summary>
    /// <param name="member">The interface member containing a redundant modifier.</param>
    /// <param name="expectedMember">The member after removing public.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public void M();", "void M();")]
    [Arguments("public int P { get; }", "int P { get; }")]
    [Arguments("public int this[int i] { get; }", "int this[int i] { get; }")]
    [Arguments("public event System.Action E { add { } remove { } }", "event System.Action E { add { } remove { } }")]
    [Arguments("public event System.Action E;", "event System.Action E;")]
    [Arguments("public class Nested { }", "class Nested { }")]
    [Arguments("public abstract void M();", "abstract void M();")]
    [Arguments("abstract public void M();", "abstract void M();")]
    [Arguments("[System.Obsolete] public void M();", "[System.Obsolete] void M();")]
    public async Task MemberModifierIsRemovedAsync(string member, string expectedMember)
    {
        var source = $"interface I {{\n    {member}\n}}";
        var expected = $"interface I {{\n    {expectedMember}\n}}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var token = root.DescendantTokens().Single(static token => token.ValueText == PublicModifier);
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RedundantModifier, token.GetLocation(), PublicModifier);
        using var container = new ContainerConfiguration().WithPart<Sst1491RedundantModifierCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies diagnostic spans on ordinary tokens do not register or apply a fix.</summary>
    /// <param name="source">The source receiving the stale diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System;")]
    [Arguments("class C { }")]
    [Arguments("System.Console.WriteLine();")]
    public async Task NonModifierLocationIsUnchangedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RedundantModifier, root.GetFirstToken().GetLocation(), PublicModifier);
        using var container = new ContainerConfiguration().WithPart<Sst1491RedundantModifierCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
