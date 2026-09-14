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

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests reference-equality fixes with stale diagnostics and operand trivia.</summary>
public class Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProviderTests
{
    /// <summary>Checks stale comparisons and unavailable framework methods do not offer an edit.</summary>
    /// <param name="expression">The expression carrying the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a")]
    [Arguments("a + b")]
    [Arguments("a == b")]
    [Arguments("a != b")]
    public async Task UnbindableComparisonDoesNotRegisterOrEditAsync(string expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ReferenceComparison", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ bool M() => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST1495", expression);
        using var container = new ContainerConfiguration().WithPart<Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks operand trivia is removed without changing the operand's tokens.</summary>
    /// <param name="operand">The operand to rewrite.</param>
    /// <param name="leading">Whether it has leading trivia.</param>
    /// <param name="trailing">Whether it has trailing trivia.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("a", false, false)]
    [Arguments("a", false, true)]
    [Arguments("a", true, false)]
    [Arguments("a", true, true)]
    [Arguments("this.a", true, true)]
    public async Task OperandTriviaIsRemovedAsync(string operand, bool leading, bool trailing)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("OperandTrivia", LanguageNames.CSharp).AddDocument("Test.cs", "class C { bool M() => a == b; }");
        var root = (await document.GetSyntaxRootAsync())!;
        var original = root.DescendantNodes().OfType<BinaryExpressionSyntax>().Single();
        var left = SyntaxFactory.ParseExpression(operand)
            .WithLeadingTrivia(leading ? SyntaxFactory.TriviaList(SyntaxFactory.Space) : default)
            .WithTrailingTrivia(trailing ? SyntaxFactory.TriviaList(SyntaxFactory.Space) : default);
        root = root.ReplaceNode(original, original.WithLeft(left));
        document = document.WithSyntaxRoot(root);
        var comparison = root.DescendantNodes().OfType<BinaryExpressionSyntax>().Single();
        var changed = TargetCodeFix.Apply(document, root, comparison, Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProvider.BuildEqualsCall);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var invocation = changedRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(invocation.ToString()).IsEqualTo($"object.Equals({operand}, b)");
    }
}
