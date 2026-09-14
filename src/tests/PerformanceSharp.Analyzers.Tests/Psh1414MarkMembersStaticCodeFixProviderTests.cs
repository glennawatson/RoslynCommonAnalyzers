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
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests static-member rewrites against stale diagnostics and recursive references.</summary>
public sealed class Psh1414MarkMembersStaticCodeFixProviderTests
{
    /// <summary>The document used to apply member edits.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies unsafe receivers and inaccessible call sites prevent every fix entry point.</summary>
    /// <param name="source">The declaration carrying a stale member diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int field; }")]
    [Arguments("class C { internal int M() => 1; }")]
    [Arguments("class C { private static int M() => 1; }")]
    [Arguments("class C { int M() => 1; }")]
    [Arguments("class C { private int M() => 1; int Call(C other) => other.M(); }")]
    [Arguments("partial class C { private int M() => 1; } partial class C { }")]
    public async Task UnsafeMemberHasNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument(DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var member = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First().Members.First();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.MarkMembersStatic, member.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1414MarkMembersStaticCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Psh1414MarkMembersStaticCodeFixProvider.Apply(document, root, model, member)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1414MarkMembersStaticCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies recursive and external this-qualified references are repaired together.</summary>
    /// <param name="modifier">An additional modifier following the private accessibility.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("new ")]
    public async Task RecursiveAndExternalReceiversAreRemovedAsync(string modifier)
    {
        var source = $"class C {{ private {modifier}int M(int n) => n == 0 ? 0 : this.M(n - 1); int Call() => this.M(1); int Other(int M) => M; }}";
        var expected = $"class C {{ private static {modifier}int M(int n) => n == 0 ? 0 : M(n - 1); int Call() => M(1); int Other(int M) => M; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument(DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.MarkMembersStatic, member.GetLocation());
        var changed = Psh1414MarkMembersStaticCodeFixProvider.Apply(document, root, model, member);
        var expectedText = (await CSharpSyntaxTree.ParseText(expected).GetRootAsync()).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedText);
        using var container = new ContainerConfiguration().WithPart<Psh1414MarkMembersStaticCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applied = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await applied.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedText);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1414MarkMembersStaticCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedText);
    }

    /// <summary>Verifies a stale diagnostic on an expression cannot select a containing member for rewriting.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExpressionDiagnosticDoesNotSelectMemberAsync()
    {
        const string Source = "class C { private int M() => 1; }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument(DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var expression = root.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ApiSelectionRules.MarkMembersStatic, expression.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1414MarkMembersStaticCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1414MarkMembersStaticCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }
}
