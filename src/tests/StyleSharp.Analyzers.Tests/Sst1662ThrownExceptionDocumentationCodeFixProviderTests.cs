// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests exception documentation insertion and incomplete diagnostic rejection.</summary>
public class Sst1662ThrownExceptionDocumentationCodeFixProviderTests
{
    /// <summary>Checks stale properties, unsupported documentation, and non-method locations cannot create edits.</summary>
    /// <param name="documentation">The documentation attached to the member.</param>
    /// <param name="member">The diagnosed declaration.</param>
    /// <param name="types">The type list; null omits its property.</param>
    /// <param name="descriptions">The description list; null omits its property.</param>
    /// <param name="retainNulls">Whether null values are stored instead of omitted.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("/// <summary>Runs.</summary>", "void M() { }", null, "When invalid.", false)]
    [Arguments("/// <summary>Runs.</summary>", "void M() { }", "", "When invalid.", false)]
    [Arguments("/// <summary>Runs.</summary>", "void M() { }", "Exception", null, false)]
    [Arguments("/// <summary>Runs.</summary>", "void M() { }", "Exception", "", false)]
    [Arguments("/// <summary>Runs.</summary>", "void M() { }", "Exception", null, true)]
    [Arguments("/// <summary>Runs.</summary>", "void M() { }", null, "When invalid.", true)]
    [Arguments("", "void M() { }", "Exception", "When invalid.", false)]
    [Arguments("/** <summary>Runs.</summary> */", "void M() { }", "Exception", "When invalid.", false)]
    [Arguments("/// <summary>Value.</summary>", "int P => 0;", "Exception", "When invalid.", false)]
    public async Task InapplicableDiagnosticDoesNotChangeDocumentationAsync(string documentation, string member, string? types, string? descriptions, bool retainNulls)
    {
        var source = $"class C\n{{\n    {documentation}\n    {member}\n}}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<MemberDeclarationSyntax>().Last();
        var properties = ImmutableDictionary<string, string?>.Empty;
        if (types is not null || retainNulls)
        {
            properties = properties.Add(Sst1662ThrownExceptionDocumentationAnalyzer.ThrownTypesKey, types);
        }

        if (descriptions is not null || retainNulls)
        {
            properties = properties.Add(Sst1662ThrownExceptionDocumentationAnalyzer.ThrownDescriptionsKey, descriptions);
        }

        var diagnostic = Diagnostic.Create(DocumentationRules.ThrownExceptionDocumentation, declaration.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1662ThrownExceptionDocumentationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Checks aligned descriptions, empty entries, and original line endings survive single and batch fixes.</summary>
    /// <param name="types">The newline-separated type names.</param>
    /// <param name="descriptions">The aligned descriptions.</param>
    /// <param name="newLine">The source line ending.</param>
    /// <param name="elements">The expected inserted elements, separated by a newline.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Exception\nArgumentException", "First.\nSecond.", "\n", "/// <exception cref=\"Exception\">First.</exception>\n/// <exception cref=\"ArgumentException\">Second.</exception>")]
    [Arguments("Exception\nArgumentException", "First.", "\r\n", "/// <exception cref=\"Exception\">First.</exception>")]
    [Arguments("\nException\n", "Ignored.\nSecond.\nIgnored.", "\n", "/// <exception cref=\"Exception\">Second.</exception>")]
    [Arguments("Exception\nArgumentException", "\nSecond.", "\n", "/// <exception cref=\"ArgumentException\">Second.</exception>")]
    public async Task DescribedTypesAreInsertedInOrderAsync(string types, string descriptions, string newLine, string elements)
    {
        var source = $"class C{newLine}{{{newLine}    /// <summary>Runs.</summary>{newLine}    void M() {{ }}{newLine}}}";
        var expectedElements = string.Join($"{newLine}    ", elements.Split('\n'));
        var expected = $"class C{newLine}{{{newLine}    /// <summary>Runs.</summary>{newLine}    {expectedElements}{newLine}    void M() {{ }}{newLine}}}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithParseOptions(new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose)).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(Sst1662ThrownExceptionDocumentationAnalyzer.ThrownTypesKey, types)
            .Add(Sst1662ThrownExceptionDocumentationAnalyzer.ThrownDescriptionsKey, descriptions);
        var diagnostic = Diagnostic.Create(DocumentationRules.ThrownExceptionDocumentation, method.Identifier.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1662ThrownExceptionDocumentationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var changes = new List<TextChange>();
        var text = await document.GetTextAsync();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }
}
