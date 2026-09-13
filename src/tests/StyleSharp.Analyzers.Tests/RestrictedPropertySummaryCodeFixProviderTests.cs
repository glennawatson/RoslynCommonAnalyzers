// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests summary text replacement and stale diagnostics through single and batch fixes.</summary>
public class RestrictedPropertySummaryCodeFixProviderTests
{
    /// <summary>Verifies only an exact leading accessor phrase in the first XML text token changes.</summary>
    /// <param name="summaryText">The original summary element.</param>
    /// <param name="expectedSummary">The expected summary element.</param>
    /// <param name="changeCount">The expected number of batch text changes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("<summary>Gets or sets the value.</summary>", "<summary>Gets the value.</summary>", 1)]
    [Arguments("<summary>  \tGets or sets the value.</summary>", "<summary>  \tGets the value.</summary>", 1)]
    [Arguments("<summary>Gets or sets <see cref=\"int\"/> values.</summary>", "<summary>Gets <see cref=\"int\"/> values.</summary>", 1)]
    [Arguments("<summary></summary>", "<summary></summary>", 0)]
    [Arguments("<summary>   </summary>", "<summary>   </summary>", 0)]
    [Arguments("<summary>Gets the value.</summary>", "<summary>Gets the value.</summary>", 0)]
    [Arguments("<summary>gets or sets the value.</summary>", "<summary>gets or sets the value.</summary>", 0)]
    [Arguments("<summary>Returns a value. Gets or sets it.</summary>", "<summary>Returns a value. Gets or sets it.</summary>", 0)]
    public async Task FirstTextTokenDeterminesReplacementAsync(string summaryText, string expectedSummary, int changeCount)
    {
        var source = $"class C\n{{\n    /// {summaryText}\n    public int Value {{ get; private set; }}\n}}";
        var expected = $"class C\n{{\n    /// {expectedSummary}\n    public int Value {{ get; private set; }}\n}}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Summary.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var summary = root.DescendantNodes(descendIntoTrivia: true).OfType<XmlElementSyntax>().Single();
        var diagnostic = Diagnostic.Create(DocumentationRules.PropertySummaryOmitsRestrictedSetter, summary.GetLocation());
        using var container = new ContainerConfiguration().WithPart<RestrictedPropertySummaryCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var applied = await RestrictedPropertySummaryCodeFixProvider.ApplyAsync(document, summary, CancellationToken.None);
        await Assert.That((await applied.GetTextAsync()).ToString()).IsEqualTo(expected);
        var changes = new List<TextChange>();
        var text = await document.GetTextAsync();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(changes.Count).IsEqualTo(changeCount);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a diagnostic outside an XML element has no single or batch fix.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticOutsideSummaryIsUnchangedAsync()
    {
        const string Source = "class C { public int Value { get; private set; } }";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Summary.cs", SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(DocumentationRules.PropertySummaryOmitsRestrictedSetter, property.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<RestrictedPropertySummaryCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changes = new List<TextChange>();
        var text = await document.GetTextAsync();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1624");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(TextChangeBatchFixAllProvider.Instance);
    }
}
