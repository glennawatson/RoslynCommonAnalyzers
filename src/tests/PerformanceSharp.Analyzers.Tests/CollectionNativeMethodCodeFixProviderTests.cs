// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests collection fix properties and stale predicate shapes.</summary>
public class CollectionNativeMethodCodeFixProviderTests
{
    /// <summary>Verifies native predicate properties control direct, registered, and batch edits.</summary>
    /// <param name="expression">The expression at the diagnostic location.</param>
    /// <param name="target">The target property, or null when its value is null.</param>
    /// <param name="membership">Whether the diagnostic requests membership rewriting.</param>
    /// <param name="expected">The expected expression, or null when unchanged.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("items.Any(x => x > 0)", null, false, null)]
    [Arguments("items.Any(x => x > 0)", "", false, null)]
    [Arguments("items.Any(x => x > 0)", "Array.FindLast", false, "System.Array.FindLast(items, x => x > 0)")]
    [Arguments("items.Any(x => x > 0)", "Exists", false, "items.Exists(x => x > 0)")]
    [Arguments("true", "Exists", false, null)]
    [Arguments("Any(x => x > 0)", "Exists", false, null)]
    [Arguments("items.Any()", "Exists", false, null)]
    [Arguments("true", null, true, null)]
    [Arguments("Any(x => x == 2)", null, true, null)]
    [Arguments("items.Any()", null, true, null)]
    [Arguments("items.Any(predicate)", null, true, null)]
    [Arguments("items.Any(x => true)", null, true, null)]
    // The provider trusts the diagnostic's equality classification after the predicate changes.
    [Arguments("items.Any(x => x > 2)", null, true, "items.Contains(2)")]
    [Arguments("items.Any(x => 1 == 2)", null, true, null)]
    [Arguments("items.Any(x => x == 2)", null, true, "items.Contains(2)")]
    public async Task PredicateShapeControlsEditsAsync(string expression, string? target, bool membership, string? expected)
    {
        var source = $"class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var reported = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var properties = ImmutableDictionary<string, string?>.Empty.Add(CollectionNativeMethodAnalyzer.TargetNameKey, target);
        var descriptor = membership ? CollectionRules.UseContainsForMembership : CollectionRules.UseCollectionNativePredicate;
        var diagnostic = Diagnostic.Create(descriptor, reported.GetLocation(), properties);
        var expectedSource = $"class C {{ object M() => {expected ?? expression}; }}";
        var direct = CollectionNativeMethodCodeFixProvider.Apply(document, root, diagnostic);
        await Assert.That((await direct.GetTextAsync()).ToString()).IsEqualTo(expectedSource);
        using var container = new ContainerConfiguration().WithPart<CollectionNativeMethodCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
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
