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
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests direct AnyAsync rewrites and stale diagnostics whose sibling method no longer binds.</summary>
public class Psh1126UseAnyAsyncOverCountAsyncCodeFixProviderTests
{
    /// <summary>Verifies both direct and batch rewrites require a recognized comparison and a boolean awaitable sibling.</summary>
    /// <param name="expression">The reported expression.</param>
    /// <param name="sibling">The available AnyAsync declaration.</param>
    /// <param name="expected">The replacement, or null when no edit applies.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("42", "", null)]
    [Arguments("1 > 0", "", null)]
    [Arguments("await CountAsync() > 0", "", null)]
    [Arguments("await this.CountAsync() > 0", "", null)]
    [Arguments("await this.CountAsync() > 0", "Task AnyAsync() => null;", null)]
    [Arguments("await this.CountAsync() > 0", "Task<int> AnyAsync() => null;", null)]
    [Arguments("await this.CountAsync() > 0", "Task<bool> AnyAsync(int value) => null;", null)]
    [Arguments("await this.CountAsync() > 0", "Task<bool> AnyAsync() => null;", "await this.AnyAsync()")]
    [Arguments("await this.CountAsync() == 0", "Task<bool> AnyAsync() => null;", "!await this.AnyAsync()")]
    public async Task SiblingBindingControlsComparisonRewriteAsync(string expression, string sibling, string? expected)
    {
        var source = $"using System.Threading.Tasks; class C {{ Task<int> CountAsync() => null; {sibling} async Task<object> M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var target = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Last().ExpressionBody!.Expression;
        var diagnostic = Diagnostic.Create(CollectionRules.UseAnyAsyncOverCountAsync, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider>(editor, diagnostic);
        var rewritten = editor.GetChangedRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Last().ExpressionBody!.Expression;
        await Assert.That(rewritten.NormalizeWhitespace().ToFullString()).IsEqualTo(SyntaxFactory.ParseExpression(expected ?? expression).NormalizeWhitespace().ToFullString());
        if (target is not BinaryExpressionSyntax binary)
        {
            return;
        }

        var changed = ReplaceNodeCodeFix.Apply(
            document,
            root,
            model,
            Diagnostic.Create(CollectionRules.UseAnyAsyncOverCountAsync, binary.GetLocation()),
            Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider.TryRewrite);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var changedExpression = changedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Last().ExpressionBody!.Expression;
        await Assert.That(changedExpression.NormalizeWhitespace().ToFullString()).IsEqualTo(rewritten.NormalizeWhitespace().ToFullString());
    }
}
