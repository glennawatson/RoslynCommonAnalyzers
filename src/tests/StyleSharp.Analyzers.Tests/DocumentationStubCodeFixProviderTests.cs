// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests documentation insertion, duplicate detection, and stale diagnostic handling.</summary>
public class DocumentationStubCodeFixProviderTests
{
    /// <summary>The diagnostic count supported by documentation stub insertion and removal.</summary>
    private const int FixableDiagnosticCount = 6;

    /// <summary>The document name used for documentation stub scenarios.</summary>
    private const string DocumentName = "Stub.cs";

    /// <summary>The diagnostic text and category used for stale-location scenarios.</summary>
    private const string DiagnosticText = "Documentation";

    /// <summary>Verifies existing elements match both their tag and optional parameter name.</summary>
    /// <param name="documentation">The method's existing documentation trivia.</param>
    /// <param name="element">The requested documentation element.</param>
    /// <param name="insert">Whether the element should be inserted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "<returns></returns>", true)]
    [Arguments("/// <summary>Runs.</summary>\n", "<returns></returns>", true)]
    [Arguments("/// <returns>A value.</returns>\n", "<returns></returns>", false)]
    [Arguments("/// <returns/>\n", "<returns></returns>", false)]
    [Arguments("/// <param name=\"value\">A value.</param>\n", "<param name=\"value\"></param>", false)]
    [Arguments("/// <param name=\"value\"/>\n", "<param name=\"value\"></param>", false)]
    [Arguments("/// <param name=\"other\">Another value.</param>\n", "<param name=\"value\"></param>", true)]
    [Arguments("/// <param name=\"Value\">A value.</param>\n", "<param name=\"value\"></param>", true)]
    [Arguments("/// <param>A value.</param>\n", "<param name=\"value\"></param>", true)]
    [Arguments("/// <param/>\n", "<param name=\"value\"></param>", true)]
    [Arguments("/// <typeparam name=\"T\">A type.</typeparam>\n", "<typeparam name=\"T\"></typeparam>", false)]
    [Arguments("/// <typeparam name=\"T\">A type.</typeparam>\n", "<param name=\"T\"></param>", true)]
    [Arguments("/// <summary>Runs.</summary>\n", "<param", true)]
    [Arguments("/// <summary>Runs.</summary>\n", "<param name=\"value>", true)]
    public async Task ExistingDocumentationControlsInsertionAsync(string documentation, string element, bool insert)
    {
        var source = $"class C\n{{\n{documentation}void M() {{ }}\n}}";
        var insertedLine = insert ? $"/// {element}\n" : string.Empty;
        var expected = $"class C\n{{\n{documentation}{insertedLine}void M() {{ }}\n}}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var changed = await DocumentationStubCodeFixProvider.InsertElementAsync(document, member, element, CancellationToken.None);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        await Assert.That(ReferenceEquals(changed, document)).IsEqualTo(!insert);
    }

    /// <summary>Verifies inserted lines retain tabs, spaces, and the document's newline convention.</summary>
    /// <param name="indentation">The indentation preceding the member.</param>
    /// <param name="newline">The document's newline sequence.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("    ", "\n")]
    [Arguments("\t", "\r\n")]
    [Arguments("", "\r\n")]
    public async Task InsertionRetainsIndentationAndNewlineAsync(string indentation, string newline)
    {
        var source = $"class C{newline}{{{newline}{indentation}/// <summary>Runs.</summary>{newline}{indentation}void M() {{ }}{newline}}}";
        var expected = $"class C{newline}{{{newline}{indentation}/// <summary>Runs.</summary>{newline}{indentation}/// <returns></returns>{newline}{indentation}void M() {{ }}{newline}}}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var changed = await DocumentationStubCodeFixProvider.InsertElementAsync(document, member, "<returns></returns>", CancellationToken.None);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies mismatched diagnostic locations cannot create parameter or type parameter stubs.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="locationKind">The node carrying the diagnostic.</param>
    /// <param name="element">The expected inserted element, or null when no action is available.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SST1611", SyntaxKind.Parameter, "<param name=\"value\"></param>")]
    [Arguments("SST1655", SyntaxKind.Parameter, "<param name=\"value\"></param>")]
    [Arguments("SST1618", SyntaxKind.TypeParameter, "<typeparam name=\"T\"></typeparam>")]
    [Arguments("SST1656", SyntaxKind.TypeParameter, "<typeparam name=\"T\"></typeparam>")]
    [Arguments("SST1615", SyntaxKind.MethodDeclaration, "<returns></returns>")]
    [Arguments("SST1611", SyntaxKind.MethodDeclaration, null)]
    [Arguments("SST1655", SyntaxKind.MethodDeclaration, null)]
    [Arguments("SST1618", SyntaxKind.MethodDeclaration, null)]
    [Arguments("SST1656", SyntaxKind.MethodDeclaration, null)]
    [Arguments("SST1606", SyntaxKind.MethodDeclaration, null)]
    [Arguments("SST1615", SyntaxKind.CompilationUnit, null)]
    public async Task DiagnosticLocationControlsStubRegistrationAsync(string id, SyntaxKind locationKind, string? element)
    {
        const string Source = "class C { void M<T>(int value) { } } class D { }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var node = root.DescendantNodesAndSelf().Single(current => current.IsKind(locationKind));
        var descriptor = new DiagnosticDescriptor(id, DiagnosticText, DiagnosticText, DiagnosticText, DiagnosticSeverity.Warning, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, node.GetLocation());
        using var container = new ContainerConfiguration().WithPart<DocumentationStubCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(element is null ? 0 : 1);
        if (element is null)
        {
            return;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo($"class C {{ /// {element}\nvoid M<T>(int value) {{ }} }} class D {{ }}");
    }

    /// <summary>Verifies each supported declaration can own an inserted documentation element.</summary>
    /// <param name="source">The original declaration.</param>
    /// <param name="kind">The declaration carrying the diagnostic.</param>
    /// <param name="expected">The expected source after insertion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }", SyntaxKind.ClassDeclaration, "/// <returns></returns>\nclass C { }")]
    [Arguments("delegate int D();", SyntaxKind.DelegateDeclaration, "/// <returns></returns>\ndelegate int D();")]
    [Arguments("class C { C() { } }", SyntaxKind.ConstructorDeclaration, "class C { /// <returns></returns>\nC() { } }")]
    [Arguments("class C { int P => 0; }", SyntaxKind.PropertyDeclaration, "class C { /// <returns></returns>\nint P => 0; }")]
    [Arguments("class C { int this[int x] => x; }", SyntaxKind.IndexerDeclaration, "class C { /// <returns></returns>\nint this[int x] => x; }")]
    [Arguments("class C { event System.Action E { add { } remove { } } }", SyntaxKind.EventDeclaration, "class C { /// <returns></returns>\nevent System.Action E { add { } remove { } } }")]
    [Arguments("enum C { A }", SyntaxKind.EnumMemberDeclaration, "enum C { /// <returns></returns>\nA }")]
    public async Task SupportedDeclarationOwnsStubAsync(string source, SyntaxKind kind, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().Single(node => node.IsKind(kind));
        var diagnostic = Diagnostic.Create(DocumentationRules.ReturnValueMustBeDocumented, member.GetLocation());
        var actions = new List<CodeAction>();
        using var container = new ContainerConfiguration().WithPart<DocumentationStubCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies removal tolerates stale diagnostics and removes complete multiline return elements.</summary>
    /// <param name="documentation">The documentation before applying the removal.</param>
    /// <param name="expectedDocumentation">The remaining documentation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "")]
    [Arguments("/// <summary>Runs.</summary>\n", "/// <summary>Runs.</summary>\n")]
    [Arguments("/// <summary>Runs.</summary>\n/// <returns>Nothing.</returns>\n", "/// <summary>Runs.</summary>\n")]
    [Arguments("/// <summary>Runs.</summary>\n/// <returns>\n/// Nothing.\n/// </returns>\n", "/// <summary>Runs.</summary>\n")]
    public async Task ReturnsRemovalHandlesAbsentAndMultilineElementsAsync(string documentation, string expectedDocumentation)
    {
        var source = $"class C\n{{\n{documentation}void M() {{ }}\n}}";
        var expected = $"class C\n{{\n{expectedDocumentation}void M() {{ }}\n}}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(DocumentationRules.VoidMustNotHaveReturn, member.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<DocumentationStubCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(WellKnownFixAllProviders.BatchFixer);
        await Assert.That(provider.FixableDiagnosticIds.Length).IsEqualTo(FixableDiagnosticCount);
    }
}
