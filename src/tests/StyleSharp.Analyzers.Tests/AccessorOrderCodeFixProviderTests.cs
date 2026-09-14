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

/// <summary>Tests accessor reordering with attributes, modifiers, and incomplete syntax.</summary>
public class AccessorOrderCodeFixProviderTests
{
    /// <summary>The document name shared by the accessor tests.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies accessor attributes and visibility move with the accessor while slot trivia stays put.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AttributesAndModifiersRetainSlotTriviaAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, "class C { int P { /* first */ private set; /* second */ [A] get; } }");
        var root = (await document.GetSyntaxRootAsync())!;
        var list = root.DescendantNodes().OfType<AccessorListSyntax>().Single();
        var changed = await AccessorOrderCodeFixProvider.ReorderAsync(document, list, CancellationToken.None);
        var reordered = (await changed.GetSyntaxRootAsync())!.DescendantNodes().OfType<AccessorListSyntax>().Single();
        await Assert.That(reordered.Accessors[0].Keyword.ValueText).IsEqualTo("get");
        await Assert.That(reordered.Accessors[0].AttributeLists.Count).IsEqualTo(1);
        await Assert.That(reordered.Accessors[1].Modifiers.Single().ValueText).IsEqualTo("private");
        await Assert.That(reordered.Accessors[0].GetLeadingTrivia().ToFullString()).IsEqualTo(list.Accessors[0].GetLeadingTrivia().ToFullString());
        await Assert.That(reordered.Accessors[1].GetLeadingTrivia().ToFullString()).IsEqualTo(list.Accessors[1].GetLeadingTrivia().ToFullString());
    }

    /// <summary>Verifies incomplete accessors without semicolons retain the trailing trivia of their slots.</summary>
    /// <param name="expressionBody">Whether the incomplete getter has an arrow body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IncompleteAccessorsRetainTrailingTriviaAsync(bool expressionBody)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, "class C { int P { set; get; } }");
        var root = (await document.GetSyntaxRootAsync())!;
        var list = root.DescendantNodes().OfType<AccessorListSyntax>().Single();
        var getter = list.Accessors[1].WithSemicolonToken(default);
        if (expressionBody)
        {
            getter = getter.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression("1")));
        }

        var setter = list.Accessors[0].WithSemicolonToken(default).WithTrailingTrivia(SyntaxFactory.Comment("/* first */"));
        list = list.WithAccessors(SyntaxFactory.List([setter, getter.WithTrailingTrivia(SyntaxFactory.Comment("/* second */"))]));
        document = document.WithSyntaxRoot(root.ReplaceNode(root.DescendantNodes().OfType<AccessorListSyntax>().Single(), list));
        list = (await document.GetSyntaxRootAsync())!.DescendantNodes().OfType<AccessorListSyntax>().Single();
        var changed = await AccessorOrderCodeFixProvider.ReorderAsync(document, list, CancellationToken.None);
        var reordered = (await changed.GetSyntaxRootAsync())!.DescendantNodes().OfType<AccessorListSyntax>().Single();
        await Assert.That(reordered.Accessors[0].Keyword.ValueText).IsEqualTo("get");
        await Assert.That(reordered.Accessors[0].GetTrailingTrivia().ToFullString()).IsEqualTo("/* first */");
        await Assert.That(reordered.Accessors[1].GetTrailingTrivia().ToFullString()).IsEqualTo("/* second */");
    }

    /// <summary>Verifies a diagnostic outside an accessor list cannot register or batch an edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnrelatedDiagnosticHasNoEditAsync()
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<AccessorOrderCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, "class C { }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(new("SST1212", "Test", "Test", "Tests", DiagnosticSeverity.Warning, true), root.GetFirstToken().GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<AccessorOrderCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }
}
