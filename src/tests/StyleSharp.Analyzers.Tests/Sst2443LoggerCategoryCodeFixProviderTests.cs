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

/// <summary>Tests generic logger category construction and diagnostics outside a type.</summary>
public class Sst2443LoggerCategoryCodeFixProviderTests
{
    /// <summary>The source document used by the edit tests.</summary>
    private const string FileName = "Test.cs";

    /// <summary>Verifies a composed batch edit preserves syntax that no longer names a type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EarlierBatchEditCanReplaceCategoryAsync()
    {
        const string Source = "class C { object M() => Other; }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FileName, Source);
        var editor = await DocumentEditor.CreateAsync(document);
        var category = editor.OriginalRoot.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(CorrectnessRules.WrongLoggerCategory, category.GetLocation());
        editor.ReplaceNode(category, static (current, _) => current.CopyAnnotationsTo(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));
        using var container = new ContainerConfiguration().WithPart<Sst2443LoggerCategoryCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo("class C { object M() => null; }");
    }

    /// <summary>Verifies generic categories retain every enclosing type parameter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GenericCategoryCarriesTypeParametersAsync()
    {
        const string Source = "class Service<T, U> { ILogger<Other> logger; }";
        const string Expected = "class Service<T, U> { ILogger<Service<T,U>> logger; }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var category = root.DescendantNodes().OfType<TypeArgumentListSyntax>().Single().Arguments.Single();
        var diagnostic = Diagnostic.Create(CorrectnessRules.WrongLoggerCategory, category.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2443LoggerCategoryCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Expected);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo("class Service<T, U> { ILogger<Service<T, U>> logger; }");
    }

    /// <summary>Verifies missing category syntax or enclosing types withhold both edit paths.</summary>
    /// <param name="source">The source at a stale category diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { object value = 1; }")]
    [Arguments("Other value;")]
    public async Task MissingCategoryContextHasNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<VariableDeclarationSyntax>().Single();
        SyntaxNode reported = declaration.Variables[0].Initializer is { } initializer ? initializer.Value : declaration.Type;
        var diagnostic = Diagnostic.Create(CorrectnessRules.WrongLoggerCategory, reported.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2443LoggerCategoryCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
