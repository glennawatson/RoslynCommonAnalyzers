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
using RoslynCommon.Analyzers.CodeFixes;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests LINQ rewrites and rejection of stale or capture-prone diagnostics.</summary>
public class LinqChainCodeFixProviderTests
{
    /// <summary>Verifies registration and both edit paths agree about a chain's applicability.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="expression">The original expression.</param>
    /// <param name="replacement">The expected expression, or null when the edit is unsafe.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("PSH1107", "42", null)]
    [Arguments("PSH1107", "values.Where(x => true)", null)]
    [Arguments("PSH1107", "values.Select(x => x).Where(x => true)", null)]
    [Arguments("PSH1107", "values.OrderBy(x => x).OrderBy(x => x).Where(x => true)", null)]
    [Arguments("PSH1107", "values.OrderByDescending(x => x).Where(x => true)", "values.Where(x => true).OrderByDescending(x => x)")]
    [Arguments("PSH1108", "42", null)]
    [Arguments("PSH1108", "values.ThenBy(x => x)", null)]
    [Arguments("PSH1108", "values.OrderBy<int, int>(x => x)", "values.ThenBy<int, int>(x => x)")]
    [Arguments("PSH1109", "42", null)]
    [Arguments("PSH1109", "values.Where(x => true)", null)]
    [Arguments("PSH1109", "values.Where(Predicate).Where(x => true)", null)]
    [Arguments("PSH1109", "values.Where(x => true).Where(Predicate)", null)]
    [Arguments("PSH1109", "values.Where(x => { return true; }).Where(y => true)", null)]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => { return true; })", null)]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => x > y)", null)]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => y is int x)", null)]
    [Arguments("PSH1109", "values.Where((x) => true).Where((y) => false)", "values.Where((x) => true && false)")]
    [Arguments("PSH1109", "values.Where(x => x).Where(y => y)", "values.Where(x => x && x)")]
    [Arguments("PSH1109", "values.Where(x => x.Read()).Where(y => y.Read())", "values.Where(x => x.Read() && x.Read())")]
    [Arguments("PSH1109", "values.Where(x => x.Value).Where(y => (y.Value))", "values.Where(x => x.Value && (x.Value))")]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => y.Read(y: y) && y.y)", "values.Where(x => true && (x.Read(y: x) && x.y))")]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => y?.y == true)", "values.Where(x => true && (x?.y == true))")]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => new { y = y }.y > 0)", "values.Where(x => true && (new { y = x }.y > 0))")]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => typeof(N.y) != null)", "values.Where(x => true && (typeof(N.y) != null))")]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => typeof(y.N) != null)", "values.Where(x => true && (typeof(x.N) != null))")]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => typeof(global::y) != null)", "values.Where(x => true && (typeof(global::y) != null))")]
    [Arguments("PSH1109", "values.Where(x => true).Where(y => typeof(y::N) != null)", "values.Where(x => true && (typeof(x::N) != null))")]
    public async Task ChainRewritePreservesBindingOrRejectsAsync(string id, string expression, string? replacement)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ object M() => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var node = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var location = id == "PSH1108" && node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
            ? access.Name.GetLocation()
            : node.GetLocation();
        var descriptor = new LinqChainAnalyzer().SupportedDiagnostics.Single(rule => rule.Id == id);
        var diagnostic = Diagnostic.Create(descriptor, location);
        using var container = new ContainerConfiguration().WithPart<LinqChainCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(replacement is null ? 0 : 1);
        var expected = SyntaxFactory.ParseExpression(replacement ?? expression).NormalizeWhitespace().ToFullString();
        var changed = ReplaceNodeCodeFix.Apply(document, root, diagnostic, LinqChainCodeFixProvider.CreateEdit);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression.NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<LinqChainCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(changedRoot.ToFullString());
        if (actions.Count == 0)
        {
            return;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var actionDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var actionRoot = (await actionDocument.GetSyntaxRootAsync())!;
        await Assert.That(actionRoot.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression.NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
    }
}
