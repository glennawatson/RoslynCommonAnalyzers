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

/// <summary>Tests each loop comparison flip and rejection of stale comparison shapes.</summary>
public class Sst2412LoopStepsAwayFromBoundCodeFixProviderTests
{
    /// <summary>Verifies an earlier batch edit replacing the comparison is preserved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EarlierBatchEditCanReplaceComparisonAsync()
    {
        const string Source = "class C { bool M(int i) => i < 0; }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var editor = await DocumentEditor.CreateAsync(document);
        var comparison = editor.OriginalRoot.DescendantNodes().OfType<BinaryExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(CorrectnessRules.LoopStepsAwayFromBound, comparison.GetLocation());
        editor.ReplaceNode(comparison, SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
        using var container = new ContainerConfiguration().WithPart<Sst2412LoopStepsAwayFromBoundCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo("class C { bool M(int i) => true; }");
    }

    /// <summary>Verifies registration and batch editing agree on supported comparisons.</summary>
    /// <param name="expression">The current loop condition.</param>
    /// <param name="expected">The flipped condition, or null when unavailable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("i <= 0", "i > 0")]
    [Arguments("i >= 0", "i < 0")]
    [Arguments("i < 0", "i >= 0")]
    [Arguments("i > 0", "i <= 0")]
    [Arguments("i == 0", null)]
    [Arguments("true", null)]
    public async Task ComparisonDeterminesFixAsync(string expression, string? expected)
    {
        var source = $"class C {{ void M() {{ for (int i = 0; {expression}; i--) {{ }} }} }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var condition = root.DescendantNodes().OfType<ForStatementSyntax>().Single().Condition!;
        var diagnostic = Diagnostic.Create(CorrectnessRules.LoopStepsAwayFromBound, condition.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2412LoopStepsAwayFromBoundCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        var expectedSource = $"class C {{ void M() {{ for (int i = 0; {expected ?? expression}; i--) {{ }} }} }}";
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expectedSource);
        if (actions.Count == 0)
        {
            return;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expectedSource);
    }
}
