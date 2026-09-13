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

/// <summary>Tests exception-filter transformations, trivia retention, and stale diagnostics.</summary>
public class Sst2009UseExceptionFilterCodeFixProviderTests
{
    /// <summary>The document name used for exception-filter syntax scenarios.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies every condition inversion and retained branch through single and batch fixes.</summary>
    /// <param name="declaration">The catch declaration, including its parentheses when present.</param>
    /// <param name="body">The original catch statements.</param>
    /// <param name="condition">The expected filter expression.</param>
    /// <param name="retainedBody">The statements kept in the catch.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("(System.Exception ex)", "if (ex.HResult == 0) throw; M();", "ex.HResult != 0", "M();")]
    [Arguments("(System.Exception ex)", "if (ex.HResult != 0) throw; M();", "ex.HResult == 0", "M();")]
    [Arguments("(System.Exception ex)", "if (ex.HResult < 0) throw; M();", "ex.HResult >= 0", "M();")]
    [Arguments("(System.Exception ex)", "if (ex.HResult <= 0) throw; M();", "ex.HResult > 0", "M();")]
    [Arguments("(System.Exception ex)", "if (ex.HResult > 0) throw; M();", "ex.HResult <= 0", "M();")]
    [Arguments("(System.Exception ex)", "if (ex.HResult >= 0) throw; M();", "ex.HResult < 0", "M();")]
    [Arguments("", "if (((!(flag)))) throw; M(); M();", "flag", "M(); M();")]
    [Arguments("", "if (flag && other) throw; M();", "!(flag && other)", "M();")]
    [Arguments("", "if (flag || other) throw; M();", "!(flag || other)", "M();")]
    [Arguments("", "if (flag) throw; else M();", "!(flag)", "M();")]
    [Arguments("", "if (flag) M(); else throw;", "flag", "M();")]
    [Arguments("", "if (flag) { throw; } else { }", "!(flag)", "")]
    [Arguments("", "if (flag) { } else { throw; }", "flag", "")]
    public async Task FilterPreservesTheHandledBranchAsync(string declaration, string body, string condition, string retainedBody)
    {
        var source = $"class C {{ void M(bool flag = false, bool other = false) {{ try {{ M(); }} catch {declaration} {{ {body} }} }} }}";
        var expected = $"class C {{ void M(bool flag = false, bool other = false) {{ try {{ M(); }} catch {declaration} when ({condition}) {{ {retainedBody} }} }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        var diagnostic = Diagnostic.Create(ModernizationRules.UseExceptionFilter, clause.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2009UseExceptionFilterCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(expected).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var applied = Sst2009UseExceptionFilterCodeFixProvider.Apply(document, root, clause);
        await Assert.That((await applied.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        var batchProvider = (IBatchFixableCodeFix)provider;
        batchProvider.RegisterBatchEdits(editor, diagnostic);
        batchProvider.RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
    }

    /// <summary>Verifies invalid catch shapes are rejected by registration, batch editing, and direct application.</summary>
    /// <param name="handler">The catch clause that cannot be transformed.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("catch when (flag) { if (flag) throw; M(); }")]
    [Arguments("catch { }")]
    [Arguments("catch { M(); if (flag) throw; }")]
    [Arguments("catch { if (flag) throw; }")]
    [Arguments("catch { if (flag) M(); M(); }")]
    [Arguments("catch { if (flag) throw new System.Exception(); M(); }")]
    [Arguments("catch { if (flag) throw; else throw; }")]
    [Arguments("catch { if (flag) M(); else M(); }")]
    [Arguments("catch { if (flag) throw; else M(); M(); }")]
    [Arguments("catch { if (flag) throw;\n#region Handle\nM();\n#endregion\n}")]
    public async Task UnsupportedCatchRemainsUnchangedAsync(string handler)
    {
        var source = $"class C {{ void M(bool flag = false) {{ try {{ M(); }} {handler} }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        var diagnostic = Diagnostic.Create(ModernizationRules.UseExceptionFilter, clause.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2009UseExceptionFilterCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
        await Assert.That(Sst2009UseExceptionFilterCodeFixProvider.Apply(document, root, clause)).IsSameReferenceAs(document);
    }

    /// <summary>Verifies a stale diagnostic outside a catch has no single or batch fix.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticOutsideCatchHasNoFixAsync()
    {
        const string Source = "class C { }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(ModernizationRules.UseExceptionFilter, root.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2009UseExceptionFilterCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies blank lines are stripped while comments and indentation retain their exact trivia.</summary>
    /// <param name="leadingTrivia">The trivia before the first surviving statement.</param>
    /// <param name="expectedTrivia">The trivia retained after moving the condition.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "")]
    [Arguments("  ", "  ")]
    [Arguments("// comment\n  ", "// comment\n  ")]
    [Arguments("  /* comment */ ", "  /* comment */ ")]
    [Arguments("\n", "")]
    [Arguments("  \n", "")]
    [Arguments("\n  \n// comment\n  ", "// comment\n  ")]
    [Arguments("\n  ", "  ")]
    public async Task FirstRemainingStatementKeepsOnlyMeaningfulTriviaAsync(string leadingTrivia, string expectedTrivia)
    {
        const string Source = "class C { void M(bool flag = false) { try { M(); } catch { if (flag) throw; M(); } } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        var survivor = clause.Block.Statements[1];
        root = root.ReplaceNode(survivor, survivor.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(leadingTrivia)));
        clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        var changed = Sst2009UseExceptionFilterCodeFixProvider.Apply(document, root, clause);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var changedClause = changedRoot.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        await Assert.That(changedClause.Block.Statements[0].GetLeadingTrivia().ToFullString()).IsEqualTo(expectedTrivia);
        await Assert.That(changedClause.Block.Statements.Count).IsEqualTo(1);
    }

    /// <summary>Verifies the newline after a typed or untyped catch moves behind its new filter.</summary>
    /// <param name="declaration">The optional catch declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("(System.Exception)")]
    public async Task FilterRetainsTheCatchHeaderLineBreakAsync(string declaration)
    {
        var source = $"class C {{ void M(bool flag = false) {{ try {{ M(); }} catch{declaration}\n{{ if (flag) {{\n\n  M(); }} else throw; }} }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var clause = root.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        var changed = Sst2009UseExceptionFilterCodeFixProvider.Apply(document, root, clause);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var changedClause = changedRoot.DescendantNodes().OfType<CatchClauseSyntax>().Single();
        await Assert.That(changedClause.Filter!.CloseParenToken.TrailingTrivia.ToFullString()).IsEqualTo("\n");
        await Assert.That(changedClause.Block.Statements[0].GetLeadingTrivia().ToFullString()).IsEqualTo("  ");
    }
}
