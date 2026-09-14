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

/// <summary>Tests accessor prefixes and stale property-summary fixes.</summary>
public class PropertySummaryCodeFixProviderTests
{
    /// <summary>Verifies individual and batch fixes agree, including summaries without text.</summary>
    /// <param name="accessors">The property's accessors.</param>
    /// <param name="summaryText">The original XML summary.</param>
    /// <param name="prefix">The expected accessor prefix.</param>
    /// <param name="expectedSummary">The resulting XML summary.</param>
    /// <param name="changeCount">The number of expected text changes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("get; set;", "<summary>The value.</summary>", "Gets or sets ", "<summary>Gets or sets the value.</summary>", 1)]
    [Arguments("get;", "<summary>The value.</summary>", "Gets ", "<summary>Gets the value.</summary>", 1)]
    [Arguments("set { }", "<summary>The value.</summary>", "Sets ", "<summary>Sets the value.</summary>", 1)]
    [Arguments("get;", "<summary></summary>", "Gets ", "<summary></summary>", 0)]
    [Arguments("get;", "<summary>   </summary>", "Gets ", "<summary>   </summary>", 0)]
    [Arguments("get;", "<summary><see cref=\"int\"/></summary>", "Gets ", "<summary><see cref=\"int\"/></summary>", 0)]
    public async Task SummaryTextControlsSingleAndBatchEditsAsync(string accessors, string summaryText, string prefix, string expectedSummary, int changeCount)
    {
        var source = $"class C\n{{\n    /// {summaryText}\n    public int Value {{ {accessors} }}\n}}";
        var expected = $"class C\n{{\n    /// {expectedSummary}\n    public int Value {{ {accessors} }}\n}}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("PropertySummary", LanguageNames.CSharp).AddDocument("Summary.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var summary = root.DescendantNodes(descendIntoTrivia: true).OfType<XmlElementSyntax>().Single();
        var diagnostic = Diagnostic.Create(DocumentationRules.PropertySummaryAccessors, summary.GetLocation());
        using var container = new ContainerConfiguration().WithPart<PropertySummaryCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo($"Prefix summary with '{prefix.TrimEnd()}'");
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var applied = await TextChangeCodeFix.ApplyAsync(document, diagnostic, PropertySummaryCodeFixProvider.RegisterTextChanges, CancellationToken.None);
        await Assert.That((await applied.GetTextAsync()).ToString()).IsEqualTo(expected);
        var changes = new List<TextChange>();
        var text = await document.GetTextAsync();
        PropertySummaryCodeFixProvider.RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(changes.Count).IsEqualTo(changeCount);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies stale locations outside a property summary produce no action or text changes.</summary>
    /// <param name="source">The source containing the stale location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { public int Value { get; } }")]
    [Arguments("class C\n{\n    /// <summary>A method.</summary>\n    public void M() { }\n}")]
    public async Task NonPropertySummaryIsUnchangedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleSummary", LanguageNames.CSharp).AddDocument("Summary.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var location = root.DescendantNodes(descendIntoTrivia: true).OfType<XmlElementSyntax>().FirstOrDefault()?.GetLocation() ?? root.GetLocation();
        var diagnostic = Diagnostic.Create(DocumentationRules.PropertySummaryAccessors, location);
        using var container = new ContainerConfiguration().WithPart<PropertySummaryCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        var changes = new List<TextChange>();
        PropertySummaryCodeFixProvider.RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(actions).IsEmpty();
        await Assert.That(changes).IsEmpty();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1623");
        await Assert.That(provider.GetFixAllProvider()).IsTypeOf<TextChangeBatchFixAllProvider>();
    }
}
