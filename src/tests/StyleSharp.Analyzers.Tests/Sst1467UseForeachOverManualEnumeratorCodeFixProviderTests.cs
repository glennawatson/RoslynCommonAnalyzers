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

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests iteration-variable selection, batch edits, and rejection of stale enumerator loops.</summary>
public class Sst1467UseForeachOverManualEnumeratorCodeFixProviderTests
{
    /// <summary>The document name used for enumerator rewrite scenarios.</summary>
    private const string DocumentName = "Foreach.cs";

    /// <summary>Verifies the leading declaration is reused only when it is the sole Current read.</summary>
    /// <param name="body">The original loop body.</param>
    /// <param name="iterationVariable">The expected foreach variable declaration.</param>
    /// <param name="expectedBody">The expected rewritten loop body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{ }", "var item", "{ }")]
    [Arguments("Use(e.Current);", "var item", "Use(item);")]
    [Arguments("{ Use(e.Current); }", "var item", "{ Use(item); }")]
    [Arguments("{ int value = e.Current; Use(value); }", "int value", "{ Use(value); }")]
    [Arguments("{ var value = e.Current; }", "var value", "{ }")]
    [Arguments("{ var item = e.Current; Use(item); }", "var item", "{ Use(item); }")]
    [Arguments("{ var value = e.Current; Use(e.Current); }", "var item", "{ var value = item; Use(item); }")]
    [Arguments("{ int value = e.Current, other = 0; Use(value); }", "var item", "{ int value = item, other = 0; Use(value); }")]
    [Arguments("{ using var value = e.Current; Use(value); }", "var item", "{ using var value = item; Use(value); }")]
    [Arguments("{ const int value = e.Current; Use(value); }", "var item", "{ const int value = item; Use(value); }")]
    [Arguments("{ int value; Use(e.Current); }", "var item", "{ int value; Use(item); }")]
    [Arguments("{ var value = 1; Use(e.Current); }", "var item", "{ var value = 1; Use(item); }")]
    [Arguments("{ var value = (e.Current); Use(value); }", "var item", "{ var value = (item); Use(value); }")]
    [Arguments("{ Use(e.Current + e.Current); }", "var item", "{ Use(item + item); }")]
    public async Task IterationVariableRetainsBodySemanticsAsync(string body, string iterationVariable, string expectedBody)
    {
        var source = $"class C {{ void M() {{ var e = values.GetEnumerator(); while (e.MoveNext()) {body} }} }}";
        var expected = $"class C {{ void M() {{ foreach ({iterationVariable} in values) {expectedBody} }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var loop = root.DescendantNodes().OfType<WhileStatementSyntax>().Single();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.UseForeachOverManualEnumerator, loop.WhileKeyword.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(expected).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var applied = Sst1467UseForeachOverManualEnumeratorCodeFixProvider.Apply(document, root, diagnostic);
        await Assert.That((await applied.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
    }

    /// <summary>Verifies invalid patterns and conflicting fallback names have no single, direct, or batch fix.</summary>
    /// <param name="statements">The statements containing the stale or unsupported loop.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("var e = values.GetEnumerator(); while (true) { }")]
    [Arguments("while (e.MoveNext()) { }")]
    [Arguments("var e = values; while (e.MoveNext()) { }")]
    [Arguments("var e = values.GetEnumerator(); while (e.MoveNext()) { e.Dispose(); }")]
    [Arguments("var e = values.GetEnumerator(); while (e.MoveNext()) { e.Current++; }")]
    [Arguments("var e = item.GetEnumerator(); while (e.MoveNext()) { Use(e.Current); }")]
    [Arguments("var e = values.GetEnumerator(); while (e.MoveNext()) { Use(e.Current + item); }")]
    [Arguments("var e = values.GetEnumerator(); while (e.MoveNext()) { var item = e.Current; Use(e.Current); }")]
    [Arguments("var e = values.GetEnumerator();\n#region Walk\nwhile (e.MoveNext()) { Use(e.Current); }\n#endregion\n")]
    public async Task UnsupportedLoopRemainsUnchangedAsync(string statements)
    {
        var source = $"class C {{ void M() {{ {statements} }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var loop = root.DescendantNodes().OfType<WhileStatementSyntax>().FirstOrDefault();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.UseForeachOverManualEnumerator, loop?.WhileKeyword.GetLocation() ?? root.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Sst1467UseForeachOverManualEnumeratorCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies replacement within a switch section preserves neighboring statements and source trivia.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SwitchSectionRetainsSurroundingStatementsAsync()
    {
        const string Source =
            "class C { void M() { switch (choice) { default: Before(); /* enumerate */ var e = GetValues().GetEnumerator(); while (e.MoveNext()) { Use(e.Current); } After(); break; } } }";
        const string Expected =
            "class C { void M() { switch (choice) { default: Before(); /* enumerate */ foreach (var item in GetValues()) { Use(item); } After(); break; } } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var loop = root.DescendantNodes().OfType<WhileStatementSyntax>().Single();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.UseForeachOverManualEnumerator, loop.WhileKeyword.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(Expected).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1467");
        await Assert.That(provider.GetFixAllProvider()).IsTypeOf<BatchEditFixAllProvider>();
    }

    /// <summary>Verifies a queued batch callback preserves a loop already changed by another edit.</summary>
    /// <param name="replacement">The statement substituted before the foreach callback executes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("for (;;) { }")]
    [Arguments("while (true) { }")]
    [Arguments("while (e.MoveNext()) { e.Dispose(); }")]
    [Arguments("while (e.MoveNext()) { Use(e.Current + item); }")]
    public async Task BatchCallbackRetainsUnsupportedReplacementAsync(string replacement)
    {
        const string Source = "class C { void M() { var e = values.GetEnumerator(); while (e.MoveNext()) { Use(e.Current); } } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var editor = await DocumentEditor.CreateAsync(document);
        var loop = editor.OriginalRoot.DescendantNodes().OfType<WhileStatementSyntax>().Single();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.UseForeachOverManualEnumerator, loop.WhileKeyword.GetLocation());
        editor.ReplaceNode(loop, (current, _) => current.CopyAnnotationsTo(SyntaxFactory.ParseStatement(replacement)));
        using var container = new ContainerConfiguration().WithPart<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>().CreateContainer();
        BatchEditRegistration.Register<Sst1467UseForeachOverManualEnumeratorCodeFixProvider>(editor, diagnostic);
        var expected = SyntaxFactory.ParseCompilationUnit($"class C {{ void M() {{ {replacement} }} }}").NormalizeWhitespace().ToFullString();
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
    }
}
