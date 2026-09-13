// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests configured stream-read fixes and rejected diagnostic shapes.</summary>
public class Sst2446DiscardedStreamReadCodeFixProviderTests
{
    /// <summary>The document name used by the code-fix tests.</summary>
    private const string TestFileName = "Test.cs";

    /// <summary>The discarded stream-read diagnostic identifier.</summary>
    private const string DiagnosticId = "SST2446";

    /// <summary>Checks stale diagnostics and non-discarded reads produce neither an action nor a batch edit.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int value = 1;", "1")]
    [Arguments("await stream.FlushAsync();", "stream.FlushAsync()")]
    [Arguments("await stream.ReadAsync(buffer, 0, 1);", "stream.ReadAsync(buffer, 0, 1)")]
    [Arguments("var value = await stream.ReadAsync(buffer, 0, 1).ConfigureAwait(false);", "stream.ReadAsync(buffer, 0, 1)")]
    [Arguments("var value = stream.ReadAsync(buffer, 0, 1).ConfigureAwait;", "stream.ReadAsync(buffer, 0, 1)")]
    [Arguments("var value = stream.ReadAsync(buffer, 0, 1).ToString();", "stream.ReadAsync(buffer, 0, 1)")]
    public async Task UnsupportedReadShapeIsUnchangedAsync(string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ReadShape", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(TestFileName, $"class C {{ async System.Threading.Tasks.Task M(System.IO.Stream stream, byte[] buffer) {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, target);
        using var container = new ContainerConfiguration().WithPart<Sst2446DiscardedStreamReadCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks the replacement API must exist as a method on the stream type.</summary>
    /// <param name="stub">The available stream metadata shape.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("namespace System.IO { class Stream {} }")]
    [Arguments("namespace System.IO { class Stream { public int ReadExactlyAsync; } }")]
    public async Task MissingReadExactlyMethodPreventsFixAsync(string stub)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ReadApi", LanguageNames.CSharp)
            .AddDocument(TestFileName, $"class C {{ async void M() {{ await stream.ReadAsync().ConfigureAwait(false); }} }} {stub}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "stream.ReadAsync()");
        using var container = new ContainerConfiguration().WithPart<Sst2446DiscardedStreamReadCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks parenthesized and inherited calls preserve their syntax when renamed.</summary>
    /// <param name="body">The discarded read statement.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <param name="expected">The rewritten statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("await ((stream.ReadAsync(buffer, 0, 1)).ConfigureAwait(false));", "ReadAsync", "await ((stream.ReadExactlyAsync(buffer, 0, 1)).ConfigureAwait(false));")]
    [Arguments("await ReadAsync(buffer, 0, 1).ConfigureAwait(false);", "ReadAsync", "await ReadExactlyAsync(buffer, 0, 1).ConfigureAwait(false);")]
    public async Task ConfiguredReadIsRenamedInSingleAndBatchFixAsync(string body, string target, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ReadRewrite", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(TestFileName, $"class C : System.IO.MemoryStream {{ async System.Threading.Tasks.Task M(System.IO.Stream stream, byte[] buffer) {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, target);
        using var container = new ContainerConfiguration().WithPart<Sst2446DiscardedStreamReadCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit($"class C : System.IO.MemoryStream {{ async System.Threading.Tasks.Task M(System.IO.Stream stream, byte[] buffer) {{ {expected} }} }}");

        // Compare every public child; parsed singleton statement lists can have an extra internal wrapper.
        await Assert.That(SyntaxFactory.AreEquivalent((await changedDocument.GetSyntaxRootAsync())!, expectedRoot, ignoreChildNode: static _ => false)).IsTrue();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(SyntaxFactory.AreEquivalent(editor.GetChangedRoot(), expectedRoot, ignoreChildNode: static _ => false)).IsTrue();
    }
}
