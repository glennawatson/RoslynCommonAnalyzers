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

/// <summary>Tests stray semicolon removal and rejection of required semicolons.</summary>
public class Sst2259RemoveStrayEmptyStatementCodeFixProviderTests
{
    /// <summary>Verifies all type shapes retain their children and trailing comments.</summary>
    /// <param name="declaration">The declaration before its stray semicolon.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C<T>(int value) where T : class { int P => value; }")]
    [Arguments("struct C<T>(int value) where T : class { int P => value; }")]
    [Arguments("interface I<T> where T : class { T P { get; } }")]
    [Arguments("record C<T>(T Value) where T : class { }")]
    [Arguments("record struct C<T>(T Value) where T : class { }")]
    [Arguments("enum E : byte { A, B }")]
    public async Task TypeSemicolonIsRemovedAsync(string declaration)
    {
        var source = $"{declaration}; // trailing\n";
        var expected = $"{declaration} // trailing\n";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.RemoveStrayEmptyStatement, root.GetLastToken().GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2259RemoveStrayEmptyStatementCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies stale diagnostics and semicolons required by the grammar have no fix.</summary>
    /// <param name="source">The source with a diagnostic on its last nonempty token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("record C(int Value);")]
    [Arguments("System.Console.WriteLine();")]
    public async Task RequiredOrMissingSemicolonIsUnchangedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.RemoveStrayEmptyStatement, root.GetLastToken().GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2259RemoveStrayEmptyStatementCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
