// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests switch-expression fixes when diagnostics no longer describe a valid candidate.</summary>
public class Sst2201PreferSwitchExpressionCodeFixProviderTests
{
    /// <summary>Checks missing switches reject registration and unsupported switches remain unchanged.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <param name="actionCount">The current number of registered actions.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("return 0;", "return", 0)]
    [Arguments("switch (value) { default: return 0; }", "switch", 1)]
    [Arguments("switch (value) { case 0: break; default: return 0; }", "switch", 1)]
    public async Task StaleSwitchDoesNotChangeDocumentAsync(string body, string target, int actionCount)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleSwitch", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ int M(int value) {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2201", target);
        using var container = new ContainerConfiguration().WithPart<Sst2201PreferSwitchExpressionCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(actionCount);
        await Assert.That(Sst2201PreferSwitchExpressionCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks declaration patterns become expression arms in single and batch edits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PatternLabelBecomesExpressionArmAsync()
    {
        using var workspace = new AdhocWorkspace();
        const string Source = "class C { int M(object value) { switch (value) { case int number: return number; default: throw new System.Exception(); } } }";
        var document = workspace.AddProject("PatternSwitch", LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2201", "number: return");
        var expected = SyntaxFactory.ParseCompilationUnit("class C { int M(object value) { return value switch { int number => number, _ => throw new System.Exception() }; } }");
        var changed = Sst2201PreferSwitchExpressionCodeFixProvider.Apply(document, root, diagnostic);

        // Compare every public child; parsed singleton statement lists can have an extra internal wrapper.
        await Assert.That(SyntaxFactory.AreEquivalent((await changed.GetSyntaxRootAsync())!, expected, ignoreChildNode: static _ => false)).IsTrue();
        using var container = new ContainerConfiguration().WithPart<Sst2201PreferSwitchExpressionCodeFixProvider>().CreateContainer();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)container.GetExport<CodeFixProvider>()).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(SyntaxFactory.AreEquivalent(editor.GetChangedRoot(), expected, ignoreChildNode: static _ => false)).IsTrue();
    }
}
