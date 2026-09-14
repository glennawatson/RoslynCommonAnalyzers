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

/// <summary>Tests assembly-lookup code-fix applicability for stale and top-level diagnostics.</summary>
public class PreferTypeofAssemblyCodeFixUnitTest
{
    /// <summary>Verifies unsupported locations offer no action and leave batch edits unchanged.</summary>
    /// <param name="source">The source containing a stale diagnostic location.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("class C { object M() => System.Reflection.Assembly.GetCallingAssembly(); }")]
    [Arguments("class C { object M() => System.Reflection.Assembly.GetExecutingAssembly(1); }")]
    [Arguments("System.Reflection.Assembly.GetExecutingAssembly();")]
    public async Task UnsupportedLocationsDoNotRegisterOrApplyAsync(string source, CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("AssemblyLookup", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var location = root.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault()?.GetLocation() ?? root.GetLocation();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.PreferTypeofAssembly, location, "C");
        using var container = new ContainerConfiguration().WithPart<Psh1404PreferTypeofAssemblyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);

        await provider.RegisterCodeFixesAsync(context);
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
        BatchEditRegistration.Register<Psh1404PreferTypeofAssemblyCodeFixProvider>(editor, diagnostic);

        await Assert.That(actions).IsEmpty();
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies a direct top-level rewrite leaves the original document intact.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TopLevelApplyReturnsOriginalDocumentAsync(CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("AssemblyLookup", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From("System.Reflection.Assembly.GetExecutingAssembly();"));
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var diagnostic = Diagnostic.Create(ApiSelectionRules.PreferTypeofAssembly, invocation.GetLocation());
        var changed = ReplaceNodeCodeFix.Apply(document, root, diagnostic, Psh1404PreferTypeofAssemblyCodeFixProvider.TryRewrite);

        await Assert.That(changed).IsSameReferenceAs(document);
    }
}
