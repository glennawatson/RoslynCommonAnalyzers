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

/// <summary>Tests direct and stale pass-through state-machine rewrites.</summary>
public class Psh1311RemovePassThroughStateMachineCodeFixProviderTests
{
    /// <summary>Verifies the direct entry point preserves unsupported methods and rewrites pass-through methods.</summary>
    /// <param name="method">The original method.</param>
    /// <param name="expectedMethod">The method after applying the fix.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("async Task M() => await Task.CompletedTask;", "Task M() => Task.CompletedTask;")]
    [Arguments("/* keep */ async public Task M() => await Task.CompletedTask;", "/* keep */ public Task M() => Task.CompletedTask;")]
    [Arguments("Task M() => Task.CompletedTask;", "Task M() => Task.CompletedTask;")]
    public async Task DirectRewritePreservesMethodContractAsync(string method, string expectedMethod)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", $"using System.Threading.Tasks; class C {{ {method} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var changed = Psh1311RemovePassThroughStateMachineCodeFixProvider.Apply(document, root, declaration);
        var expected = await CSharpSyntaxTree.ParseText($"using System.Threading.Tasks; class C {{ {expectedMethod} }}").GetRootAsync();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expected.NormalizeWhitespace().ToFullString());
        if (method != expectedMethod)
        {
            return;
        }

        await Assert.That(changed).IsSameReferenceAs(document);
    }

    /// <summary>Verifies stale method and unrelated declaration diagnostics do not register or batch an edit.</summary>
    /// <param name="selectClass">Whether the stale span identifies a class instead of a method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StaleDiagnosticDoesNotRewriteAsync(bool selectClass)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Psh1311RemovePassThroughStateMachineCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", "class C { void M() { } }");
        var root = (await document.GetSyntaxRootAsync())!;
        SyntaxNode node = selectClass ? root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single() : root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(new("PSH1311", "Test", "Test", "Tests", DiagnosticSeverity.Warning, true), node.GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1311RemovePassThroughStateMachineCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }
}
