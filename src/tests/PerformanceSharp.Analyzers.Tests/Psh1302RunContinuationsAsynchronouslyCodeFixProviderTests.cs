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
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests completion-source edits with stale diagnostics and optional or named options.</summary>
public class Psh1302RunContinuationsAsynchronouslyCodeFixProviderTests
{
    /// <summary>Verifies unresolved constructors and non-creation expressions decline registration and batch edits.</summary>
    /// <param name="expression">The expression at the stale diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("42")]
    [Arguments("new Missing()")]
    public async Task UnresolvedCreationHasNoRewriteAsync(string expression)
    {
        var source = $"class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.RunContinuationsAsynchronously, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1302RunContinuationsAsynchronouslyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies optional options are appended and named or opaque options are found by parameter binding.</summary>
    /// <param name="creation">The original creation.</param>
    /// <param name="expected">The expected rewritten creation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new TaskCompletionSource<int> { }", "new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously) { }")]
    [Arguments("new TaskCompletionSource<int>()", "new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)")]
    [Arguments("new TaskCompletionSource<int>(options: TaskCreationOptions.None)", "new TaskCompletionSource<int>(options: TaskCreationOptions.RunContinuationsAsynchronously)")]
    [Arguments("new TaskCompletionSource<int>(state: null)", "new TaskCompletionSource<int>(state: null, TaskCreationOptions.RunContinuationsAsynchronously)")]
    [Arguments("new TaskCompletionSource<int>(state: null, options: TaskCreationOptions.None)", "new TaskCompletionSource<int>(state: null, options: TaskCreationOptions.RunContinuationsAsynchronously)")]
    [Arguments("new TaskCompletionSource<int>(null, options)", "new TaskCompletionSource<int>(null, options | TaskCreationOptions.RunContinuationsAsynchronously)")]
    public async Task OptionalAndNamedOptionsAreRewrittenAsync(string creation, string expected)
    {
        var source = $$"""
            using System.Threading.Tasks;
            namespace System.Threading.Tasks
            {
                enum TaskCreationOptions { None = 0, RunContinuationsAsynchronously = 64 }
                class TaskCompletionSource<T>
                {
                    public TaskCompletionSource(TaskCreationOptions options = 0) { }
                    public TaskCompletionSource(object state, TaskCreationOptions options = 0) { }
                }
            }
            class C { object M(TaskCreationOptions options) => {{creation}}; }
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ConcurrencyRules.RunContinuationsAsynchronously, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1302RunContinuationsAsynchronouslyCodeFixProvider>().CreateContainer();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)container.GetExport<CodeFixProvider>()).RegisterBatchEdits(editor, diagnostic);
        var rewritten = editor.GetChangedRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        await Assert.That(rewritten.NormalizeWhitespace().ToFullString()).IsEqualTo(SyntaxFactory.ParseExpression(expected).NormalizeWhitespace().ToFullString());
    }

    /// <summary>Verifies unavailable and shadowed short enum names produce the fully qualified flag.</summary>
    /// <param name="shadow">A competing type declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("class TaskCreationOptions { }")]
    [Arguments("enum TaskCreationOptions { None }")]
    public async Task UnavailableShortEnumNameUsesQualifiedFlagAsync(string shadow)
    {
        var source = $"class C {{ {shadow} object M() => new System.Threading.Tasks.TaskCompletionSource<int>(); }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ConcurrencyRules.RunContinuationsAsynchronously, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1302RunContinuationsAsynchronouslyCodeFixProvider>().CreateContainer();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)container.GetExport<CodeFixProvider>()).RegisterBatchEdits(editor, diagnostic);
        var rewritten = editor.GetChangedRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        await Assert.That(rewritten.ArgumentList!.Arguments.Single().Expression.ToString()).IsEqualTo("global::System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously");
    }
}
