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

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests logical-operator fixes for stale diagnostics, patterns, and preserved layout.</summary>
public class Sst2288UseLogicalOperatorCodeFixProviderTests
{
    /// <summary>The source document name shared by the logical-operator scenarios.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies a stale conditional diagnostic cannot register or perform an edit.</summary>
    /// <param name="expression">The expression now at the diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("b")]
    [Arguments("b ? true : false")]
    [Arguments("b ? b : b")]
    public async Task StaleDiagnosticDoesNotRegisterOrEditAsync(string expression)
    {
        var source = $"class C {{ bool M(bool b) => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UseLogicalOperatorOverConditional, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2288UseLogicalOperatorCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2288UseLogicalOperatorCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies negation uses only syntax supported by the document's language version.</summary>
    /// <param name="expression">The conditional to rewrite.</param>
    /// <param name="version">The language version of the document.</param>
    /// <param name="expected">The equivalent logical expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value is string ? false : b", LanguageVersion.CSharp8, "!(value is string) && b")]
    [Arguments("value is null ? false : b", LanguageVersion.CSharp8, "!(value is null) && b")]
    [Arguments("value is not string ? false : b", LanguageVersion.CSharp9, "value is string && b")]
    [Arguments("value is null ? false : b", LanguageVersion.CSharp9, "value is not null && b")]
    [Arguments("value is { } ? false : b", LanguageVersion.CSharp9, "value is not { } && b")]
    [Arguments("!((b)) ? false : b", LanguageVersion.CSharp8, "b && b")]
    public async Task NegationHonorsLanguageVersionAsync(string expression, LanguageVersion version, string expected)
    {
        var source = $"class C {{ bool M(object value, bool b) => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(version))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UseLogicalOperatorOverConditional, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2288UseLogicalOperatorCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit($"class C {{ bool M(object value, bool b) => {expected}; }}").NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
        var compilation = (await changed.Project.GetCompilationAsync())!;
        await Assert.That(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2288UseLogicalOperatorCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expectedRoot);
    }

    /// <summary>Verifies a multiline fix retains indentation while dropping non-whitespace before the operator.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultilineOperatorKeepsOnlyLeadingWhitespaceAsync()
    {
        const string Source = "class C { bool M(bool a, bool b) => a /*condition*/\n    /*branch*/ ? b : false; }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.UseLogicalOperatorOverConditional, target.GetLocation());
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2288UseLogicalOperatorCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo("class C { bool M(bool a, bool b) => a /*condition*/\n     && b; }");
    }
}
