// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests opening delimiter fixes when diagnostics refer to unsupported tokens or unsafe trivia.</summary>
public class OpeningParenOnDeclarationLineCodeFixProviderTests
{
    /// <summary>Verifies unsupported tokens are rejected before registering or applying a change.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnsupportedTokenHasNoFixAsync()
    {
        const string Source = "class C { }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var token = root.GetFirstToken();
        var diagnostic = Diagnostic.Create(ReadabilityRules.OpeningParenOnDeclarationLine, token.GetLocation());
        using var container = new ContainerConfiguration().WithPart<OpeningParenOnDeclarationLineCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changed = await OpeningParenOnDeclarationLineCodeFixProvider.FixAsync(document, token.Span, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document);
        var changes = new List<TextChange>();
        OpeningParenOnDeclarationLineCodeFixProvider.RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1110");
        await Assert.That(provider.GetFixAllProvider()).IsTypeOf<TextChangeBatchFixAllProvider>();
    }

    /// <summary>Verifies a supported delimiter cannot remove preceding comments or move before the first token.</summary>
    /// <param name="source">The document containing the delimiter.</param>
    /// <param name="delimiter">The delimiter selected by the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("(1);", "(")]
    [Arguments("[A] class C { }", "[")]
    [Arguments("class C { void M /* Preserve */\n() { } }", "(")]
    [Arguments("class C { int this /* Preserve */\n[int index] => index; }", "[")]
    [Arguments("class C { void M // Preserve\n() { } }", "(")]
    public async Task MissingPredecessorOrCommentLeavesDocumentUnchangedAsync(string source, string delimiter)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var token = root.FindToken(source.IndexOf(delimiter, StringComparison.Ordinal));
        var diagnostic = Diagnostic.Create(ReadabilityRules.OpeningParenOnDeclarationLine, token.GetLocation());
        using var container = new ContainerConfiguration().WithPart<OpeningParenOnDeclarationLineCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution.GetDocument(document.Id) ?? document;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(source);
        var direct = await OpeningParenOnDeclarationLineCodeFixProvider.FixAsync(document, token.Span, CancellationToken.None);
        await Assert.That(direct).IsSameReferenceAs(document);
        var changes = new List<TextChange>();
        OpeningParenOnDeclarationLineCodeFixProvider.RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }
}
