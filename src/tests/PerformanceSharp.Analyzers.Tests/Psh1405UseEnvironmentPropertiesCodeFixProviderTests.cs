// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests Environment property replacements and stale diagnostic handling.</summary>
public class Psh1405UseEnvironmentPropertiesCodeFixProviderTests
{
    /// <summary>Verifies individual and batch actions replace each recognized chain and preserve its trivia.</summary>
    /// <param name="expression">The original chain.</param>
    /// <param name="property">The direct Environment property.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Process.GetCurrentProcess().Id", "ProcessId")]
    [Arguments("Process.GetCurrentProcess().MainModule.FileName", "ProcessPath")]
    [Arguments("Thread.CurrentThread.ManagedThreadId", "CurrentManagedThreadId")]
    [Arguments("GetCurrentProcess().Id", "ProcessId")]
    [Arguments("global::System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName", "ProcessPath")]
    public async Task RecognizedChainIsReplacedAsync(string expression, string property)
    {
        var source = $"class C {{ object M() => /* before */ {expression} /* after */; }}";
        var expected = $"class C {{ object M() => /* before */ System.Environment.{property} /* after */; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var access = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().First();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.UseEnvironmentProperties, access.GetLocation(), property);
        using var container = new ContainerConfiguration().WithPart<Psh1405UseEnvironmentPropertiesCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.FixableDiagnosticIds).Contains("PSH1405");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(BatchEditFixAllProvider.Instance);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo($"Use System.Environment.{property}");
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a stale diagnostic offers no action and does not edit the document.</summary>
    /// <param name="expression">An unsupported expression at the diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("42")]
    [Arguments("process.Id")]
    [Arguments("Process.GetCurrentProcess(1).Id")]
    public async Task UnsupportedExpressionIsUnchangedAsync(string expression)
    {
        var source = $"class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var node = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(ApiSelectionRules.UseEnvironmentProperties, node.GetLocation(), "ProcessId");
        using var container = new ContainerConfiguration().WithPart<Psh1405UseEnvironmentPropertiesCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
        if (node is MemberAccessExpressionSyntax access)
        {
            await Assert.That(Psh1405UseEnvironmentPropertiesCodeFixProvider.Apply(document, root, access)).IsSameReferenceAs(document);
        }
    }
}
