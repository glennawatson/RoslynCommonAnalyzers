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

/// <summary>Tests case-conversion fixes when their reported comparison has changed.</summary>
public class Psh1200AvoidCaseConversionComparisonCodeFixProviderTests
{
    /// <summary>Checks direct application handles both binary and method-call comparisons.</summary>
    /// <param name="expression">The comparison expression.</param>
    /// <param name="expected">The expected replacement expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a.ToLower() == b.ToLower()", "string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase)")]
    [Arguments("a.ToUpperInvariant() != b.ToUpperInvariant()", "!string.Equals(a, b, System.StringComparison.InvariantCultureIgnoreCase)")]
    [Arguments("a.ToLower().Equals(b.ToLower())", "string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase)")]
    public async Task DirectApplicationBuildsTheComparisonAsync(string expression, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("DirectCaseComparison", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ bool M(string a, string b) => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var comparison = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var changed = ReplaceNodeCodeFix.Apply(
            document,
            root,
            Diagnostic.Create(StringRules.AvoidCaseConversionComparison, comparison.GetLocation()),
            Psh1200AvoidCaseConversionComparisonCodeFixProvider.TryRewrite);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression.ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks unsupported expressions remain unchanged through every fix entry point.</summary>
    /// <param name="expression">The stale comparison expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a")]
    [Arguments("a == b.ToLower()")]
    [Arguments("a.ToLower() == b")]
    [Arguments("a.ToLower() == b.ToUpper()")]
    [Arguments("Equals(a, b)")]
    [Arguments("a.Equals(b)")]
    [Arguments("a.ToLower().Equals(b.ToUpper())")]
    public async Task ChangedComparisonDoesNotRegisterOrEditAsync(string expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ChangedCaseComparison", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ bool M(string a, string b) => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var comparison = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(StringRules.AvoidCaseConversionComparison, comparison.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1200AvoidCaseConversionComparisonCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(ReplaceNodeCodeFix.Apply(
            document,
            root,
            Diagnostic.Create(StringRules.AvoidCaseConversionComparison, comparison.GetLocation()),
            Psh1200AvoidCaseConversionComparisonCodeFixProvider.TryRewrite)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1200AvoidCaseConversionComparisonCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }
}
