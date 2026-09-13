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

/// <summary>Tests unused-member removal through individual actions and batch edits.</summary>
public class Sst1440PrivateMemberUsageCodeFixProviderTests
{
    /// <summary>The source document name.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The identifier targeted by removal diagnostics.</summary>
    private const string UnusedName = "Unused";

    /// <summary>A type with no remaining members.</summary>
    private const string EmptyType = "class C { }";

    /// <summary>Verifies removal preserves the remaining members and combined declarators.</summary>
    /// <param name="source">The member containing the target identifier.</param>
    /// <param name="expected">The resulting type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { private void Unused() { } void Keep() { } }", "class C { void Keep() { } }")]
    [Arguments("class C { private int Unused; }", EmptyType)]
    [Arguments("class C { private int Unused, kept; }", "class C { private int kept; }")]
    [Arguments("class C { private int kept, Unused, last; }", "class C { private int kept, last; }")]
    [Arguments("class C { private event System.Action Unused; }", EmptyType)]
    [Arguments("class C { private event System.Action Unused, kept; }", "class C { private event System.Action kept; }")]
    [Arguments("class C { private event System.Action kept, Unused; }", "class C { private event System.Action kept; }")]
    public async Task RemovesOnlyTargetMemberAsync(string source, string expected)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst1440PrivateMemberUsageCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantTokens().Single(static token => token.ValueText == UnusedName);
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveUnusedPrivateMember, target.GetLocation());
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(expected).NormalizeWhitespace().ToFullString();
        var changed = Sst1440PrivateMemberUsageCodeFixProvider.Apply(document, root, diagnostic);
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applied = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await applied.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1440");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(BatchEditFixAllProvider.Instance);
    }

    /// <summary>Verifies stale diagnostics on nonmembers do not register or apply a fix.</summary>
    /// <param name="source">The source containing the stale target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using Unused;")]
    [Arguments("class C { void M() { int Unused = 0; } }")]
    public async Task NonmemberTargetHasNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst1440PrivateMemberUsageCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantTokens().Single(static token => token.ValueText == UnusedName);
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveUnusedPrivateMember, target.GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Sst1440PrivateMemberUsageCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies a detached variable has no declaration that can be removed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedVariableLeavesDocumentUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(EmptyType));
        var variable = SyntaxFactory.VariableDeclarator(UnusedName);
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveUnusedPrivateMember, variable.Identifier.GetLocation());
        await Assert.That(Sst1440PrivateMemberUsageCodeFixProvider.Apply(document, variable, diagnostic)).IsSameReferenceAs(document);
    }

    /// <summary>Verifies removal that would consume the supplied root leaves the document unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemovingEntireRootLeavesDocumentUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(EmptyType));
        var root = (ClassDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(EmptyType)!;
        var diagnostic = Diagnostic.Create(MaintainabilityRules.RemoveUnusedPrivateMember, root.Identifier.GetLocation());
        await Assert.That(Sst1440PrivateMemberUsageCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
    }
}
