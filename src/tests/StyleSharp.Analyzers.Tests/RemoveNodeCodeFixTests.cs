// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests node removal selectors and registration with stale diagnostics.</summary>
public class RemoveNodeCodeFixTests
{
    /// <summary>The synthetic diagnostic used to exercise removal selectors.</summary>
    private const string DiagnosticId = "SST0000";

    /// <summary>Checks exact-node and ancestor selectors accept only the requested syntax kind.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task SelectorsRespectRequestedNodeKindAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C { void M() { return; } }");
        var statement = root.DescendantNodes().OfType<ReturnStatementSyntax>().Single();
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "return;");
        await Assert.That(RemoveNodeCodeFix.Node<ReturnStatementSyntax>(root, diagnostic)!.Value.Node).IsSameReferenceAs(statement);
        await Assert.That(RemoveNodeCodeFix.Node<ClassDeclarationSyntax>(root, diagnostic)).IsNull();
        await Assert.That(RemoveNodeCodeFix.Ancestor<MethodDeclarationSyntax>(root, diagnostic)!.Value.Node).IsSameReferenceAs(statement.Parent!.Parent!);
        await Assert.That(RemoveNodeCodeFix.Ancestor<UsingDirectiveSyntax>(root, diagnostic)).IsNull();
    }

    /// <summary>Checks selectors may decline both registration and batch editing.</summary>
    /// <param name="semantic">Whether the selector requests a semantic model.</param>
    /// <param name="applicable">Whether the current diagnostic selects a node.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task RegistrationAndBatchUseTheSameSelectionAsync(bool semantic, bool applicable)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("Removal", LanguageNames.CSharp).AddDocument("Test.cs", "class C { void M() { return; } }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "return;");
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        var editor = await DocumentEditor.CreateAsync(document);
        if (semantic)
        {
            Func<SyntaxNode, SemanticModel, Diagnostic, NodeRemoval?> selector = (syntax, _, issue) => applicable ? RemoveNodeCodeFix.Node<ReturnStatementSyntax>(syntax, issue) : null;
            await RemoveNodeCodeFix.RegisterAsync(context, "Remove return", "RemoveReturn", selector);
            RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, selector);
        }
        else
        {
            Func<SyntaxNode, Diagnostic, NodeRemoval?> selector = (syntax, issue) => applicable ? RemoveNodeCodeFix.Node<ReturnStatementSyntax>(syntax, issue) : null;
            await RemoveNodeCodeFix.RegisterAsync(context, "Remove return", "RemoveReturn", selector);
            RemoveNodeCodeFix.ApplyBatchEdit(editor, diagnostic, selector);
        }

        await Assert.That(actions.Count).IsEqualTo(applicable ? 1 : 0);
        var expected = applicable ? "class C { void M() { } }" : root.ToFullString();
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
        if (!applicable)
        {
            return;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetSyntaxRootAsync())!.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Checks deleting the compilation unit leaves the document intact when Roslyn returns no root.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task RemovingRootLeavesDocumentIntactAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("Removal", LanguageNames.CSharp).AddDocument("Test.cs", "class C {}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "class C {}");
        var actions = new List<CodeAction>();
        await RemoveNodeCodeFix.RegisterAsync(
            new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None),
            "Remove root",
            "RemoveRoot",
            static (syntax, _) => new NodeRemoval(syntax));
        var operations = await actions.Single().GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetSyntaxRootAsync())!.ToFullString()).IsEqualTo(root.ToFullString());
    }
}
