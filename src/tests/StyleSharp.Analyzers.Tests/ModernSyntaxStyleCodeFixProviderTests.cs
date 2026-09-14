// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.CodeFixes;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests modern syntax edits when diagnostic syntax has changed.</summary>
public class ModernSyntaxStyleCodeFixProviderTests
{
    /// <summary>Checks every entry point rejects a stale or unsupported diagnostic.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="expression">The current expression.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("SST0000", "1", "1")]
    [Arguments("SST2202", "1", "1")]
    [Arguments("SST2202", "new C { }", "new")]
    [Arguments("SST2203", "1", "1")]
    [Arguments("SST2203", "M(1)", "1")]
    [Arguments("SST2203", "M(1 + 2)", "1 + 2")]
    [Arguments("SST2204", "1", "1")]
    [Arguments("SST2204", "M(1)", "M(1)")]
    [Arguments("SST2204", "text.Substring()", "Substring")]
    [Arguments("SST2204", "text.Substring(1, 2, 3)", "Substring")]
    public async Task StaleDiagnosticLeavesDocumentUnchangedAsync(string id, string expression, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ModernSyntax", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ object M() => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, id, target);
        using var container = new ContainerConfiguration().WithPart<ModernSyntaxStyleCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(0);
        await Assert.That(ReplaceNodeCodeFix.Apply(document, root, diagnostic, ModernSyntaxStyleCodeFixProvider.TryRewrite)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<ModernSyntaxStyleCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks range lengths and argument modifiers survive direct syntax rewrites.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="expression">The expression to rewrite.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <param name="expected">The rewritten expression.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("SST2204", "text.Substring(2, 3)", "Substring", "text[2..(2 + 3)]")]
    [Arguments("SST2203", "M(index: values.Length - 1)", "values.Length - 1", "M(index: ^1)")]
    [Arguments("SST2203", "M(ref values.Length - 1)", "values.Length - 1", "M(ref ^1)")]
    public async Task ReplacementPreservesArgumentShapeAsync(string id, string expression, string target, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ModernSyntax", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ object M() => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, id, target);
        var changed = ReplaceNodeCodeFix.Apply(document, root, diagnostic, ModernSyntaxStyleCodeFixProvider.TryRewrite);
        await Assert.That((await changed.GetSyntaxRootAsync())!.ToFullString()).Contains(expected);
    }
}
