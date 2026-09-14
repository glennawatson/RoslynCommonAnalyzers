// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.CodeFixes;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests direct count rewrites and stale comparison diagnostics.</summary>
public class Psh1119UseAnyOverCountCodeFixProviderTests
{
    /// <summary>Verifies direct application handles empty, nonempty, and unrelated comparisons.</summary>
    /// <param name="expression">The comparison to rewrite.</param>
    /// <param name="expected">The resulting expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("items.Count() > 0", "items.Any()")]
    [Arguments("items.Count() == 0", "!items.Any()")]
    [Arguments("items.Count() > 2", "items.Count() > 2")]
    public async Task DirectComparisonEditAsync(string expression, string expected)
    {
        var source = $"class C {{ bool M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var comparison = root.DescendantNodes().OfType<BinaryExpressionSyntax>().Single();
        var changed = ReplaceNodeCodeFix.Apply(document, root, Diagnostic.Create(CollectionRules.UseAnyOverCount, comparison.GetLocation()), Psh1119UseAnyOverCountCodeFixProvider.TryRewrite);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo($"class C {{ bool M() => {expected}; }}");
    }

    /// <summary>Verifies obsolete diagnostic locations do not register actions or edits.</summary>
    /// <param name="expression">The current expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("items.Count() > 2")]
    [Arguments("true")]
    public async Task UnsupportedComparisonHasNoFixAsync(string expression)
    {
        var source = $"class C {{ bool M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var reported = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(CollectionRules.UseAnyOverCount, reported.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1119UseAnyOverCountCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1119UseAnyOverCountCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
