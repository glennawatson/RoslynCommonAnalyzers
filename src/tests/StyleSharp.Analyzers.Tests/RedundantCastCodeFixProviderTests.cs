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

/// <summary>Tests cast removal through direct edits and stale diagnostics.</summary>
public class RedundantCastCodeFixProviderTests
{
    /// <summary>The source document used by the edit tests.</summary>
    private const string FileName = "Test.cs";

    /// <summary>Verifies composed batch edits preserve an expression that is no longer a conversion.</summary>
    /// <param name="replacement">The expression supplied by an earlier edit.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("value + 1")]
    [Arguments("Convert(value)")]
    public async Task EarlierBatchEditCanRemoveConversionAsync(string replacement)
    {
        const string Source = "class C { int M(int value) => (int)value; }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FileName, Source);
        var editor = await DocumentEditor.CreateAsync(document);
        var cast = editor.OriginalRoot.DescendantNodes().OfType<CastExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ReadabilityRules.NoRedundantCast, cast.GetLocation());
        editor.ReplaceNode(cast, (current, _) => current.CopyAnnotationsTo(SyntaxFactory.ParseExpression(replacement)));
        BatchEditRegistration.Register<RedundantCastCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo($"class C {{ int M(int value) => {replacement}; }}");
    }

    /// <summary>Verifies unsupported expressions receive neither a lightbulb nor a batch edit.</summary>
    /// <param name="expression">The expression at the diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("value + 1")]
    [Arguments("Convert(value)")]
    public async Task UnrelatedExpressionHasNoFixAsync(string expression)
    {
        var source = $"class C {{ object M(object value) => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var reported = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(ReadabilityRules.NoRedundantCast, reported.GetLocation());
        using var container = new ContainerConfiguration().WithPart<RedundantCastCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<RedundantCastCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies direct cast removal retains the surrounding comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DirectCastEditRetainsTriviaAsync()
    {
        const string Source = "class C { int M(int value) => /* before */ (int)value /* after */; }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(FileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var cast = root.DescendantNodes().OfType<CastExpressionSyntax>().Single();
        var changed = TargetCodeFix.Apply(document, root, cast, RedundantCastCodeFixProvider.RemoveConversion);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo("class C { int M(int value) => /* before */ value /* after */; }");
    }
}
