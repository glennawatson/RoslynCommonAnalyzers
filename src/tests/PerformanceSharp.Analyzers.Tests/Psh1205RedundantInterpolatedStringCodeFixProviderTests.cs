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
using Microsoft.CodeAnalysis.Text;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests interpolation replacement syntax and stale diagnostic handling.</summary>
public class Psh1205RedundantInterpolatedStringCodeFixProviderTests
{
    /// <summary>The document name shared by the replacement fixtures.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Verifies individual, direct, and batch replacements preserve expression grouping and surrounding trivia.</summary>
    /// <param name="expression">The original interpolated expression.</param>
    /// <param name="replacement">The expected replacement expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("$\"{value}\"", "value")]
    [Arguments("$\"{\"literal\"}\"", "\"literal\"")]
    [Arguments("$\"{value.Name}\"", "value.Name")]
    [Arguments("$\"{value?.Name}\"", "value?.Name")]
    [Arguments("$\"{Get()}\"", "Get()")]
    [Arguments("$\"{values[0]}\"", "values[0]")]
    [Arguments("$\"{(value)}\"", "(value)")]
    [Arguments("$\"{$\"nested\"}\"", "$\"nested\"")]
    [Arguments("$\"{new string('x', 1)}\"", "new string('x', 1)")]
    [Arguments("$\"{(left, right)}\"", "(left, right)")]
    [Arguments("$\"{left + right}\"", "(left + right)")]
    [Arguments("$\"{ /* inner */ value /* inner */ }\"", "value")]
    [Arguments("$\"\"", "\"\"")]
    [Arguments("$\"x\"", "\"x\"")]
    [Arguments("$\"before {{x}} after\"", "\"before {x} after\"")]
    [Arguments("$@\"line\ntext\"", "\"line\\ntext\"")]
    public async Task RecognizedInterpolationIsReplacedAsync(string expression, string replacement)
    {
        var source = $"class C {{ object M() => /* before */ {expression} /* after */; }}";
        var expected = $"class C {{ object M() => /* before */ {replacement} /* after */; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var interpolated = root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>().First();
        var diagnostic = Diagnostic.Create(StringRules.RedundantInterpolatedString, interpolated.GetLocation(), "the value itself");
        using var container = new ContainerConfiguration().WithPart<Psh1205RedundantInterpolatedStringCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.FixableDiagnosticIds).Contains("PSH1205");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(BatchEditFixAllProvider.Instance);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo("Remove the redundant interpolation");
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var applied = Psh1205RedundantInterpolatedStringCodeFixProvider.Apply(document, root, interpolated);
        await Assert.That((await applied.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies stale diagnostics for unsupported syntax register no actions or batch edits.</summary>
    /// <param name="expression">The unsupported expression at the diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("$\"prefix {value}\"")]
    [Arguments("$\"{value:F2}\"")]
    [Arguments("$\"{value,10}\"")]
    [Arguments("$\"{left}{right}\"")]
    public async Task UnsupportedInterpolationRemainsUnchangedAsync(string expression)
    {
        var source = $"class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var node = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(StringRules.RedundantInterpolatedString, node.GetLocation(), "the value itself");
        using var container = new ContainerConfiguration().WithPart<Psh1205RedundantInterpolatedStringCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
        if (node is InterpolatedStringExpressionSyntax interpolated)
        {
            await Assert.That(Psh1205RedundantInterpolatedStringCodeFixProvider.Apply(document, root, interpolated)).IsSameReferenceAs(document);
        }
    }

    /// <summary>Verifies separately constructed text nodes concatenate and unescape each segment independently.</summary>
    /// <param name="first">The first text segment value.</param>
    /// <param name="second">The second text segment value.</param>
    /// <param name="expected">The expected combined literal value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a{{", "b}}", "a{b}")]
    [Arguments("", "x", "x")]
    [Arguments("{x}", "a", "{x}a")]
    [Arguments("a{{x{y}z}", "}}b", "a{x{y}z}}b")]
    [Arguments("{{{", "}}}", "{{}}")]
    public async Task SeparateTextSegmentsPreserveLiteralValueAsync(string first, string second, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From("class C { string M() => $\"\"; }"));
        var root = (await document.GetSyntaxRootAsync())!;
        var original = root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>().Single();
        var firstText = SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(default, SyntaxKind.InterpolatedStringTextToken, first, first, default));
        var secondText = SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(default, SyntaxKind.InterpolatedStringTextToken, second, second, default));
        var interpolated = original.WithContents(SyntaxFactory.List<InterpolatedStringContentSyntax>([firstText, secondText]));
        root = root.ReplaceNode(original, interpolated);
        document = document.WithSyntaxRoot(root);
        interpolated = root.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>().Single();
        var changed = Psh1205RedundantInterpolatedStringCodeFixProvider.Apply(document, root, interpolated);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var literal = changedRoot.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();
        await Assert.That(literal.Token.ValueText).IsEqualTo(expected);
    }
}
