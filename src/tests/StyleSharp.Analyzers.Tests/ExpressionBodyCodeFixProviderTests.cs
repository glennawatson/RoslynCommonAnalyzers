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

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests expression-body code fixes after the reported member changes.</summary>
public sealed class ExpressionBodyCodeFixProviderTests
{
    /// <summary>Verifies unsupported member bodies receive no action or batch rewrite.</summary>
    /// <param name="declaration">The member at the stale diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int field;")]
    [Arguments("void M() { }")]
    [Arguments("C() { }")]
    [Arguments("public static C operator +(C left, C right) { throw null; }")]
    [Arguments("public static implicit operator int(C value) { throw null; }")]
    [Arguments("int P { get; set; }")]
    [Arguments("int this[int i] { get => i; set { } }")]
    [Arguments("void M() { void Local() { } }")]
    public async Task StaleMemberHasNoExpressionBodyFixAsync(string declaration)
    {
        var source = $"class C {{ {declaration} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().Members.Single();
        var target = (SyntaxNode?)member.DescendantNodes().OfType<LocalFunctionStatementSyntax>().FirstOrDefault() ?? member;
        var token = target switch
        {
            MethodDeclarationSyntax method => method.Identifier,
            ConstructorDeclarationSyntax constructor => constructor.Identifier,
            OperatorDeclarationSyntax operation => operation.OperatorToken,
            ConversionOperatorDeclarationSyntax conversion => conversion.ImplicitOrExplicitKeyword,
            PropertyDeclarationSyntax property => property.Identifier,
            IndexerDeclarationSyntax indexer => indexer.ThisKeyword,
            LocalFunctionStatementSyntax local => local.Identifier,
            _ => target.GetFirstToken(),
        };
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UseExpressionBodyForMethod, token.GetLocation());
        using var container = new ContainerConfiguration().WithPart<ExpressionBodyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies multiline expressions and multiple signature comments survive collapsing a block.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultilineExpressionRetainsCommentsAndCarriageReturnsAsync()
    {
        const string Source = "class C\r\n{\r\n    int M() /* first */ /* second */\r\n    {\r\n        return 1 + \r\n            2;\r\n    }\r\n}";
        const string Expected = "class C\r\n{\r\n    int M() /* first *//* second */ => 1 + \r\n            2;\r\n}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UseExpressionBodyForMethod, method.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<ExpressionBodyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Expected);
    }
}
