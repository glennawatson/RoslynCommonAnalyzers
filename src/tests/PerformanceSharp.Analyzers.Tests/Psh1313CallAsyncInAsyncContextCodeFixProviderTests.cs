// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests async sibling binding and stale diagnostics across single and batch edits.</summary>
public class Psh1313CallAsyncInAsyncContextCodeFixProviderTests
{
    /// <summary>The source document name shared by the fixtures.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The async sibling named by synthetic diagnostics.</summary>
    private const string AsyncSiblingName = "ReadAsync";

    /// <summary>Verifies only an invocation that binds to the resolved sibling receives an await.</summary>
    /// <param name="expression">The reported expression.</param>
    /// <param name="members">The available synchronous and asynchronous members.</param>
    /// <param name="replacement">The expected replacement, or null when the fix is refused.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("42", "", null)]
    [Arguments("Missing()", "", null)]
    [Arguments("Read()", "public int Read() => 1;", null)]
    [Arguments("Read()", "public int Read() => 1; public Task<int> ReadAsync() => null;", "await ReadAsync()")]
    [Arguments("this.Read()", "public int Read() => 1; public Task<int> ReadAsync() => null;", "await this.ReadAsync()")]
    [Arguments("this?.Read()", "public int Read() => 1; public Task<int> ReadAsync() => null;", null)]
    [Arguments("Read(value: 1)", "public int Read(int value) => value; public Task<int> ReadAsync(int other) => null;", null)]
    [Arguments("Read(1)", "public int Read(int value) => value; public Task<int> ReadAsync(int value, bool optional = false) => null; public Task<int> ReadAsync(int value) => null;", null)]
    [Arguments("Read(1)", "public int Read(int value) => value; public Task<int> ReadAsync(int value, bool optional = false) => null;", "await ReadAsync(1)")]
    [Arguments("Read<int>()", "public int Read<T>() => 1; public Task<int> ReadAsync<T>() => null;", null)]
    [Arguments("(Read)()", "public int Read() => 1; public Task<int> ReadAsync() => null;", null)]
    [Arguments("Read!()", "public int Read() => 1; public Task<int> ReadAsync() => null;", null)]
    public async Task SiblingBindingControlsSingleAndBatchEditsAsync(string expression, string members, string? replacement)
    {
        var source = $$"""
            using System.Threading.Tasks;
            class C
            {
                {{members}}
                async Task<int> M() => {{expression}};
            }
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var body = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "M").ExpressionBody!.Expression;
        var target = body is ConditionalAccessExpressionSyntax conditional ? conditional.WhenNotNull : body;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.CallAsyncInAsyncContext, target.GetLocation(), AsyncSiblingName);
        using var container = new ContainerConfiguration().WithPart<Psh1313CallAsyncInAsyncContextCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(replacement is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1313CallAsyncInAsyncContextCodeFixProvider>(editor, diagnostic);
        var changed = ReplaceNodeCodeFix.Apply(document, root, model, diagnostic, Psh1313CallAsyncInAsyncContextCodeFixProvider.TryRewrite);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(editor.GetChangedRoot().ToFullString());

        if (replacement is null)
        {
            await Assert.That(changed).IsSameReferenceAs(document);
            await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
            return;
        }

        var changedBody = editor.GetChangedRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "M").ExpressionBody!.Expression;
        await Assert.That(changedBody.NormalizeWhitespace().ToFullString()).IsEqualTo(replacement);
        await Assert.That(provider.FixableDiagnosticIds).Contains("PSH1313");
        await Assert.That(provider.GetFixAllProvider()).IsTypeOf<BatchEditFixAllProvider>();
        await Assert.That(actions[0].Title).IsEqualTo("Await the async overload");
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo(nameof(Psh1313CallAsyncInAsyncContextCodeFixProvider));
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var actionDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var actionRoot = (await actionDocument.GetSyntaxRootAsync())!;
        await Assert.That(actionRoot.NormalizeWhitespace().ToFullString()).IsEqualTo(editor.GetChangedRoot().NormalizeWhitespace().ToFullString());
    }

    /// <summary>Verifies stale diagnostics cannot insert await in a synchronous function.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SynchronousContextRefusesSingleAndBatchEditsAsync()
    {
        const string Source = "using System.Threading.Tasks; class C { public int Read() => 1; public Task<int> ReadAsync() => null; int M() => Read(); }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ConcurrencyRules.CallAsyncInAsyncContext, invocation.GetLocation(), AsyncSiblingName);
        using var container = new ContainerConfiguration().WithPart<Psh1313CallAsyncInAsyncContextCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(ReplaceNodeCodeFix.Apply(document, root, model, diagnostic, Psh1313CallAsyncInAsyncContextCodeFixProvider.TryRewrite)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1313CallAsyncInAsyncContextCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies an unavailable task framework prevents sibling resolution.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingTaskMetadataRefusesSingleAndBatchEditsAsync()
    {
        const string Source = "class C { int Read() => 1; async void M() { Read(); } }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(ConcurrencyRules.CallAsyncInAsyncContext, invocation.GetLocation(), AsyncSiblingName);
        using var container = new ContainerConfiguration().WithPart<Psh1313CallAsyncInAsyncContextCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(ReplaceNodeCodeFix.Apply(document, root, model, diagnostic, Psh1313CallAsyncInAsyncContextCodeFixProvider.TryRewrite)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1313CallAsyncInAsyncContextCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies a diagnostic moved onto a declaration is ignored by registration and batch editing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclarationDiagnosticRefusesRegistrationAndBatchEditsAsync()
    {
        const string Source = "class C { async void M() { } }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ConcurrencyRules.CallAsyncInAsyncContext, method.GetLocation(), AsyncSiblingName);
        using var container = new ContainerConfiguration().WithPart<Psh1313CallAsyncInAsyncContextCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1313CallAsyncInAsyncContextCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }
}
