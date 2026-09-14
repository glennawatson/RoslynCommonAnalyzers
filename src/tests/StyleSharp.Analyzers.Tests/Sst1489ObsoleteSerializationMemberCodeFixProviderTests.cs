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

/// <summary>Tests serialization-member removal and rejection of stale diagnostic locations.</summary>
public class Sst1489ObsoleteSerializationMemberCodeFixProviderTests
{
    /// <summary>The source document name shared by the serialization-removal scenarios.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies single and batch fixes remove only the reported member and retain type attributes.</summary>
    /// <param name="member">The obsolete constructor or method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("protected C(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }")]
    [Arguments("public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }")]
    public async Task ReportedMemberIsRemovedAsync(string member)
    {
        var source = $"[System.Serializable] class C : System.Exception {{ public C() {{ }} {member} public int Value => 1; }}";
        const string Expected = "[System.Serializable] class C : System.Exception { public C() { } public int Value => 1; }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().Members[1];
        var diagnostic = Diagnostic.Create(MaintainabilityRules.ObsoleteSerializationMember, declaration.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1489ObsoleteSerializationMemberCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1489");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(BatchEditFixAllProvider.Instance);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(Expected).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
    }

    /// <summary>Verifies locations outside a constructor or method offer no fix and no batch edit.</summary>
    /// <param name="source">The source containing a stale member location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("class C { public int Value; }")]
    [Arguments("class C { public int Value => 1; }")]
    [Arguments("using System;")]
    public async Task UnsupportedMemberIsUnchangedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MemberDeclarationSyntax>().LastOrDefault();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.ObsoleteSerializationMember, member?.GetLocation() ?? root.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1489ObsoleteSerializationMemberCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies direct application preserves the document when removing the supplied root.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemovingRootPreservesDocumentAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From("class C { }"));
        var member = SyntaxFactory.ParseMemberDeclaration("void M() { }")!;
        await Assert.That(Sst1489ObsoleteSerializationMemberCodeFixProvider.Apply(document, member, member)).IsSameReferenceAs(document);
    }
}
