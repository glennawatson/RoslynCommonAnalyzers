// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests null-check folding when a diagnostic outlives the expression it described.</summary>
public class Sst2285FoldNullCheckIntoConditionalAccessCodeFixProviderTests
{
    /// <summary>Verifies registration and batch edits reject expressions that cannot be folded.</summary>
    /// <param name="expression">The replacement expression carrying the stale diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("true")]
    [Arguments("text != null")]
    [Arguments("text != null || text.Length > 0")]
    [Arguments("text != null && count > 0")]
    [Arguments("text != null && text.Length != 0")]
    public async Task StaleDiagnosticDoesNotRegisterOrEditAsync(string expression)
    {
        var source = $"class C {{ bool M(string text, int count) => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.FoldNullCheckIntoConditionalAccess, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2285FoldNullCheckIntoConditionalAccessCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2285FoldNullCheckIntoConditionalAccessCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
