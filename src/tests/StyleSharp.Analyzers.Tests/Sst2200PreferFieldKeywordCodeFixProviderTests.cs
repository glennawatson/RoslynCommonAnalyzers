// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests field-keyword application and stale diagnostic handling.</summary>
public class Sst2200PreferFieldKeywordCodeFixProviderTests
{
    /// <summary>The project name used by the field-keyword tests.</summary>
    private const string ProjectName = "FieldKeyword";

    /// <summary>The source document name.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>A property whose explicit backing field has an initializer.</summary>
    private const string InitializedSource = "class C { private int _value = 42; public int Value { get => this._value; set => this._value = System.Math.Max(0, value); } }";

    /// <summary>Verifies either application entry point preserves the initializer and rewrites qualified references.</summary>
    /// <param name="useFieldName">Whether the caller supplies the cached field name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ApplicationPreservesInitializerAsync(bool useFieldName)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(ProjectName, LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, InitializedSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        var changed = useFieldName
            ? await Sst2200PreferFieldKeywordCodeFixProvider.ApplyAsync(document, root, model, property, "_value", CancellationToken.None)
            : await Sst2200PreferFieldKeywordCodeFixProvider.ApplyAsync(document, root, model, property, CancellationToken.None);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var expected = SyntaxFactory.ParseCompilationUnit(
            "class C { public int Value { get => field; set => field = System.Math.Max(0, value); } = 42; }");
        await Assert.That(changedRoot.NormalizeWhitespace().ToFullString()).IsEqualTo(expected.NormalizeWhitespace().ToFullString());
        var compilation = (await changed.Project.GetCompilationAsync())!;
        await Assert.That(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    /// <summary>Verifies a stale field name or a property without a backing field leaves the document intact.</summary>
    /// <param name="source">The current document.</param>
    /// <param name="fieldName">The previously identified field, when available.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { public int Value { get; set; } }", null)]
    [Arguments("class C { public int Value { get; set; } }", "_value")]
    [Arguments(InitializedSource, "_renamed")]
    public async Task StaleApplicationLeavesDocumentUnchangedAsync(string source, string? fieldName)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(ProjectName, LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        var changed = fieldName is null
            ? await Sst2200PreferFieldKeywordCodeFixProvider.ApplyAsync(document, root, model, property, CancellationToken.None)
            : await Sst2200PreferFieldKeywordCodeFixProvider.ApplyAsync(document, root, model, property, fieldName, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document);
    }

    /// <summary>Verifies diagnostics outside a supported property do not register an action.</summary>
    /// <param name="source">The current document containing the diagnostic target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class Value { }")]
    [Arguments("class C { public int Value { get; set; } }")]
    [Arguments("public int Value { get; set; }")]
    public async Task StaleDiagnosticRegistersNoActionAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(ProjectName, LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantTokens().Single(static token => token.ValueText == "Value");
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.PreferFieldKeyword, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2200PreferFieldKeywordCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST2200");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(WellKnownFixAllProviders.BatchFixer);
    }
}
