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

/// <summary>Tests catch removal in statement lists, embedded statements, and stale syntax.</summary>
public class Sst1470RemoveRethrowOnlyCatchCodeFixProviderTests
{
    /// <summary>The source document name shared by the catch-removal scenarios.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies single and batch fixes unwrap only where the containing grammar permits.</summary>
    /// <param name="source">The original try statement and its containing syntax.</param>
    /// <param name="expected">The syntax after removing the final catch.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { try { M(); M(); } catch { throw; } } }", "class C { void M() { M(); M(); } }")]
    [Arguments("class C { void M() { try { } catch { throw; } } }", "class C { void M() { } }")]
    [Arguments("class C { void M(int x) { switch (x) { default: try { M(x); } catch { throw; } break; } } }", "class C { void M(int x) { switch (x) { default: M(x); break; } } }")]
    [Arguments("class C { void M(int x) { switch (x) { default: try { } catch { throw; } break; } } }", "class C { void M(int x) { switch (x) { default: break; } } }")]
    [Arguments("try { System.Console.WriteLine(); System.Console.WriteLine(); } catch { throw; }", "System.Console.WriteLine(); System.Console.WriteLine();")]
    [Arguments("try { } catch { throw; }", "")]
    [Arguments("class C { void M() { if (true) try { M(); } catch { throw; } } }", "class C { void M() { if (true) M(); } }")]
    [Arguments("class C { void M() { if (true) try { M(); M(); } catch { throw; } } }", "class C { void M() { if (true) { M(); M(); } } }")]
    [Arguments("class C { void M() { if (true) try { } catch { throw; } } }", "class C { void M() { if (true) { } } }")]
    [Arguments("class C { void M() { if (true) try { int value = 0; } catch { throw; } } }", "class C { void M() { if (true) { int value = 0; } } }")]
    [Arguments("class C { void M() { if (true) try { void Local() { } } catch { throw; } } }", "class C { void M() { if (true) { void Local() { } } } }")]
    [Arguments("class C { void M() { if (true) try { label: M(); } catch { throw; } } }", "class C { void M() { if (true) { label: M(); } } }")]
    [Arguments("class C { void M() { try { M(); } catch (System.Exception) { } catch { throw; } } }", "class C { void M() { try { M(); } catch (System.Exception) { } } }")]
    [Arguments("class C { void M() { try { M(); } catch { throw; } finally { M(); } } }", "class C { void M() { try { M(); } finally { M(); } } }")]
    public async Task CatchRemovalPreservesContainingSyntaxAsync(string source, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Last();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveRethrowOnlyCatch, clause.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1470RemoveRethrowOnlyCatchCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(expected).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
    }

    /// <summary>Verifies non-catch locations, non-final handlers, directives, and changed catch bodies are rejected.</summary>
    /// <param name="source">The source containing an unsupported catch or stale location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("class C { void M() { try { } catch (System.Exception) { throw; } catch { } } }")]
    [Arguments("class C { void M() { try { } catch { M(); throw; } } }")]
    [Arguments("class C { void M() { try {\n#region Body\n M();\n#endregion\n } catch { throw; } } }")]
    public async Task UnsupportedCatchIsUnchangedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().FirstOrDefault();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveRethrowOnlyCatch, clause?.GetLocation() ?? root.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1470RemoveRethrowOnlyCatchCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies direct application cannot remove a detached catch or cross a directive boundary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedAndDirectiveBoundedCatchCannotBeAppliedAsync()
    {
        const string Source = "class C { void M() { try {\n#region Body\n M();\n#endregion\n } catch { throw; } } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        await Assert.That(Sst1470RemoveRethrowOnlyCatchCodeFixProvider.Apply(document, root, clause)).IsSameReferenceAs(document);
        await Assert.That(Sst1470RemoveRethrowOnlyCatchCodeFixProvider.Apply(document, root, SyntaxFactory.CatchClause())).IsSameReferenceAs(document);
    }

    /// <summary>Verifies a nested catch is deferred when its containing try will be unwrapped in the same pass.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NestedCatchWaitsForOuterUnwrapAsync()
    {
        const string Source = "class C { void M() { try { try { M(); } catch { throw; } } catch { throw; } } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().First();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveRethrowOnlyCatch, clause.GetLocation());
        var editor = await DocumentEditor.CreateAsync(document);
        using var container = new ContainerConfiguration().WithPart<Sst1470RemoveRethrowOnlyCatchCodeFixProvider>().CreateContainer();
        ((IBatchFixableCodeFix)container.GetExport<CodeFixProvider>()).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies a nested catch is edited when the outer try keeps its handlers.</summary>
    /// <param name="handlers">The outer catch or finally clauses that prevent unwrapping.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("finally { M(); }")]
    [Arguments("catch (System.IO.IOException) { } catch { }")]
    [Arguments("catch { M(); }")]
    public async Task NestedCatchInsideRetainedTryIsRemovedAsync(string handlers)
    {
        var source = $"class C {{ void M() {{ try {{ try {{ M(); }} catch {{ throw; }} }} {handlers} }} }}";
        var expected = $"class C {{ void M() {{ try {{ M(); }} {handlers} }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().First();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveRethrowOnlyCatch, clause.GetLocation());
        var editor = await DocumentEditor.CreateAsync(document);
        using var container = new ContainerConfiguration().WithPart<Sst1470RemoveRethrowOnlyCatchCodeFixProvider>().CreateContainer();
        ((IBatchFixableCodeFix)container.GetExport<CodeFixProvider>()).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(SyntaxFactory.ParseCompilationUnit(expected).NormalizeWhitespace().ToFullString());
    }

    /// <summary>Verifies a repeated batch edit tolerates a catch already removed from a try/finally.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RepeatedCatchRemovalKeepsFinallyAsync()
    {
        const string Source = "class C { void M() { try { M(); } catch { throw; } finally { M(); } } }";
        const string Expected = "class C { void M() { try { M(); } finally { M(); } } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveRethrowOnlyCatch, clause.GetLocation());
        var editor = await DocumentEditor.CreateAsync(document);
        using var container = new ContainerConfiguration().WithPart<Sst1470RemoveRethrowOnlyCatchCodeFixProvider>().CreateContainer();
        var provider = (IBatchFixableCodeFix)container.GetExport<CodeFixProvider>();
        provider.RegisterBatchEdits(editor, diagnostic);
        provider.RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(SyntaxFactory.ParseCompilationUnit(Expected).NormalizeWhitespace().ToFullString());
    }
}
