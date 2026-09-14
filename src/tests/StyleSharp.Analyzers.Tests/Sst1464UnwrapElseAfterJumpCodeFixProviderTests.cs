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

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests scope safety and stale batch edits for unwrapping else clauses.</summary>
public class Sst1464UnwrapElseAfterJumpCodeFixProviderTests
{
    /// <summary>The source document name.</summary>
    private const string FileName = "Test.cs";

    /// <summary>The diagnostic handled by this provider.</summary>
    private const string DiagnosticId = "SST1464";

    /// <summary>Checks every entry point leaves unsafe or stale else clauses unchanged.</summary>
    /// <param name="body">The current method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("return;", "return")]
    [Arguments("if (flag) {} else {}", "else")]
    [Arguments("while (flag) if (flag) return; else {}", "else")]
    [Arguments("if (flag) return; else { int value = 0; } Log();", "else")]
    [Arguments("if (flag) return; else { void Local() {} } Log();", "else")]
    [Arguments("if (flag) return; else {\n#region Keep\nLog();\n#endregion\n}", "else")]
    public async Task UnsafeElseDoesNotRegisterOrEditAsync(string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("UnwrapElse", LanguageNames.CSharp).AddDocument(FileName, $"class C {{ void M(bool flag) {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, target);
        using var container = new ContainerConfiguration().WithPart<Sst1464UnwrapElseAfterJumpCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Sst1464UnwrapElseAfterJumpCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks earlier edits that invalidate the target cannot cause a later batch edit to unwrap it.</summary>
    /// <param name="replacement">The replacement containing block.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{}")]
    [Arguments("{ Log(); }")]
    [Arguments("{ if (flag) return; }")]
    [Arguments("{ if (flag) {} else Log(); }")]
    [Arguments("{ if (flag) return; else { int value = 0; } Log(); }")]
    public async Task ChangedBatchTargetIsPreservedAsync(string replacement)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ChangedElse", LanguageNames.CSharp).AddDocument(FileName, "class C { void M(bool flag) { if (flag) return; else Log(); } }");
        var root = (await document.GetSyntaxRootAsync())!;
        var block = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single().Body!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "else");
        using var container = new ContainerConfiguration().WithPart<Sst1464UnwrapElseAfterJumpCodeFixProvider>().CreateContainer();
        var provider = (IBatchFixableCodeFix)container.GetExport<CodeFixProvider>();
        var editor = await DocumentEditor.CreateAsync(document);
        var updatedBlock = (BlockSyntax)SyntaxFactory.ParseStatement(replacement);
        editor.ReplaceNode(block, (current, _) => current.CopyAnnotationsTo(updatedBlock));
        provider.RegisterBatchEdits(editor, diagnostic);
        var changedBlock = editor.GetChangedRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single().Body!;
        await Assert.That(changedBlock.ToFullString()).IsEqualTo(replacement);
    }

    /// <summary>Checks trailing trivia and safe final declarations survive both fix paths.</summary>
    /// <param name="body">The original method body.</param>
    /// <param name="expected">The expected statements after unwrapping.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (flag) return; else Log();", "if (flag) return; Log();")]
    [Arguments("if (flag) { return; }\nelse {}", "if (flag) { return; }")]
    [Arguments("if (flag) return; else { int value = 0; }", "if (flag) return; int value = 0;")]
    [Arguments("if (flag) return; else { void Local() {} }", "if (flag) return; void Local() {}")]
    public async Task SafeElseProducesExpectedStatementsAsync(string body, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("SafeElse", LanguageNames.CSharp).AddDocument(FileName, $"class C {{ void M(bool flag) {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "else");
        var changed = Sst1464UnwrapElseAfterJumpCodeFixProvider.Apply(document, root, diagnostic);
        var expectedRoot = SyntaxFactory.ParseCompilationUnit($"class C {{ void M(bool flag) {{ {expected} }} }}");
        await Assert.That(SyntaxFactory.AreEquivalent((await changed.GetSyntaxRootAsync())!, expectedRoot, ignoreChildNode: static _ => false)).IsTrue();
        using var container = new ContainerConfiguration().WithPart<Sst1464UnwrapElseAfterJumpCodeFixProvider>().CreateContainer();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)container.GetExport<CodeFixProvider>()).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(SyntaxFactory.AreEquivalent(editor.GetChangedRoot(), expectedRoot, ignoreChildNode: static _ => false)).IsTrue();
    }
}
