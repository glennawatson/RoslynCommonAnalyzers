// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests disposal receivers and stale throw-helper diagnostics.</summary>
public class Psh1409ThrowHelperCodeFixProviderTests
{
    /// <summary>Checks individual and batch fixes select the instance available in each context.</summary>
    /// <param name="prefix">Source preceding the guard.</param>
    /// <param name="suffix">Source following the guard.</param>
    /// <param name="instance">The expected disposal helper's instance argument.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C<T> { static void M(bool disposed) { ", " } }", "typeof(C<T>)")]
    [Arguments("class C { void M(bool disposed) { ", " } }", "this")]
    [Arguments("class C { void M() { static void Local(bool disposed) { ", " } } }", "typeof(C)")]
    [Arguments("class C { void M() { System.Action<bool> run = static disposed => { ", " }; } }", "typeof(C)")]
    [Arguments("class C { void M() { System.Action<bool> run = disposed => { ", " }; } }", "this")]
    [Arguments("class C { void M() { void Local(bool disposed) { ", " } } }", "this")]
    [Arguments("class C { static bool disposed; static int P { get { ", " return 0; } } }", "typeof(C)")]
    [Arguments("class C { bool disposed; int P { get { ", " return 0; } } }", "this")]
    [Arguments("class C { System.Action<bool> run = disposed => { ", " }; }", "typeof(C)")]
    public async Task DisposalGuardUsesTheAvailableInstanceAsync(string prefix, string suffix, string instance)
    {
        const string Guard = "if (disposed) throw new System.ObjectDisposedException(nameof(disposed));";
        var source = $"{prefix}{Guard}{suffix}";
        var expected = $"{prefix}System.ObjectDisposedException.ThrowIf(disposed, {instance});{suffix}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var statement = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.UseThrowHelpers, statement.GetLocation(), "ThrowIf");
        using var container = new ContainerConfiguration().WithPart<Psh1409ThrowHelperCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.FixableDiagnosticIds).Contains("PSH1409");
        await Assert.That(provider.GetFixAllProvider()).IsTypeOf<BatchEditFixAllProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1409ThrowHelperCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Checks stale syntax, global statements, and unavailable helpers produce no action or batch edit.</summary>
    /// <param name="source">The complete source containing the diagnostic location.</param>
    /// <param name="hasFramework">Whether framework metadata is available.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M(object value) { return; } }", true)]
    [Arguments("class C { void M(object value) { if (value is null) return; } }", true)]
    [Arguments("class C { void M(object value) { if (value is null) throw new System.ArgumentNullException(nameof(value)); } }", false)]
    [Arguments("bool disposed = false; if (disposed) throw new System.ObjectDisposedException(nameof(disposed));", true)]
    public async Task UnsupportedContextDoesNotRegisterOrRewriteAsync(string source, bool hasFramework)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        if (hasFramework)
        {
            project = project.WithMetadataReferences(RuntimeMetadataReferences.Platform);
        }

        var document = project.AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var node = (SyntaxNode?)root.DescendantNodes().OfType<IfStatementSyntax>().FirstOrDefault()
            ?? root.DescendantNodes().OfType<ReturnStatementSyntax>().Single();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.UseThrowHelpers, node.GetLocation(), "ThrowIfNull");
        using var container = new ContainerConfiguration().WithPart<Psh1409ThrowHelperCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1409ThrowHelperCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
