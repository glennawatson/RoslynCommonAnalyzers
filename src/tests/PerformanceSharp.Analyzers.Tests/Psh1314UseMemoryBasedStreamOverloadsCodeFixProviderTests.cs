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

/// <summary>Tests memory-overload code fixes when a selected call may no longer bind.</summary>
public class Psh1314UseMemoryBasedStreamOverloadsCodeFixProviderTests
{
    /// <summary>The document name shared by the rewrite tests.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The diagnostic used to select stream calls and stale spans.</summary>
    private static readonly DiagnosticDescriptor Descriptor = new("PSH1314", "Test", "Test", "Tests", DiagnosticSeverity.Warning, true);

    /// <summary>Verifies a stale diagnostic on a declaration cannot register or batch a call rewrite.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonInvocationDiagnosticHasNoEditAsync()
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, "class C { }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(Descriptor, root.GetFirstToken().GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Verifies a direct rewrite requires both the array shape and an accessible memory overload.</summary>
    /// <param name="imports">The imports visible to extension-method lookup.</param>
    /// <param name="call">The original stream call.</param>
    /// <param name="expectedCall">The expected call after applying the fix.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System;", "stream.ReadAsync(buffer, 0, 1)", "stream.ReadAsync(buffer.AsMemory(0, 1))")]
    [Arguments("using System;", "stream.WriteAsync(buffer, 0, 1)", "stream.WriteAsync(buffer.AsMemory(0, 1))")]
    [Arguments("using System;", "stream.FlushAsync()", "stream.FlushAsync()")]
    [Arguments("", "stream.ReadAsync(buffer, 0, 1)", "stream.ReadAsync(buffer, 0, 1)")]
    public async Task DirectRewriteRequiresBindingAsync(string imports, string call, string expectedCall)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument(DocumentName, $"{imports} class C {{ void M(System.IO.Stream stream, byte[] buffer) {{ {call}; }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var changed = ReplaceNodeCodeFix.Apply(
            document,
            root,
            model,
            Diagnostic.Create(ConcurrencyRules.UseMemoryBasedStreamOverloads, invocation.GetLocation()),
            Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider.TryRewrite);
        var expected = await CSharpSyntaxTree.ParseText($"{imports} class C {{ void M(System.IO.Stream stream, byte[] buffer) {{ {expectedCall}; }} }}").GetRootAsync();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expected.NormalizeWhitespace().ToFullString());
        var diagnostic = Diagnostic.Create(Descriptor, invocation.GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(call == expectedCall ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expected.NormalizeWhitespace().ToFullString());
        if (call != expectedCall)
        {
            return;
        }

        await Assert.That(changed).IsSameReferenceAs(document);
    }

    /// <summary>Verifies a similarly named overload accepting an unrelated type is not a memory rewrite.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnrelatedBoundOverloadIsRejectedAsync()
    {
        const string Source = """
            static class Extensions { public static int AsMemory(this byte[] buffer, int offset, int count) => 0; }
            class StreamLike { public void ReadAsync(byte[] buffer, int offset, int count) { } public void ReadAsync(int value) { } }
            class C { void M(StreamLike stream, byte[] buffer) { stream.ReadAsync(buffer, 0, 1); } }
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(ReplaceNodeCodeFix.Apply(
            document,
            root,
            model,
            Diagnostic.Create(ConcurrencyRules.UseMemoryBasedStreamOverloads, invocation.GetLocation()),
            Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider.TryRewrite)).IsSameReferenceAs(document);
    }
}
