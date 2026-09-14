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

/// <summary>Tests concrete-type replacements and stale declaration shapes.</summary>
public class Psh1415UseConcreteTypeCodeFixProviderTests
{
    /// <summary>Verifies registration, batch editing, and direct application agree on declaration eligibility.</summary>
    /// <param name="statement">The local declaration.</param>
    /// <param name="expected">The replacement type, or null when the declaration is rejected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("IList<int> values = new List<int>();", "List<int>")]
    [Arguments("IList<int> values;", null)]
    [Arguments("IList<int> values = null;", null)]
    [Arguments("IList<int> values = new List<int>(), other = new List<int>();", null)]
    [Arguments("object values = new T();", null)]
    [Arguments("object values = new Missing();", null)]
    public async Task DeclarationShapeControlsEditsAsync(string statement, string? expected)
    {
        var source = $$"""using System.Collections.Generic; class C { void M<T>() where T : new() { {{statement}} } }""";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var declared = root.DescendantNodes().OfType<VariableDeclarationSyntax>().Single().Type;
        var diagnostic = Diagnostic.Create(ApiSelectionRules.UseConcreteType, declared.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1415UseConcreteTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        var changed = Psh1415UseConcreteTypeCodeFixProvider.Apply(document, root, model, declared);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(editor.GetChangedRoot().ToFullString());
        if (expected is null)
        {
            await Assert.That(changed).IsSameReferenceAs(document);
            return;
        }

        await Assert.That(editor.GetChangedRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().Single().Type.ToString()).IsEqualTo(expected);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var actionDocument = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var actionRoot = (await actionDocument.GetSyntaxRootAsync())!;
        await Assert.That(actionRoot.DescendantNodes().OfType<VariableDeclarationSyntax>().Single().Type.ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies diagnostics on methods and return types are rejected.</summary>
    /// <param name="onType">Whether the diagnostic points to the return type instead of the method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NonvariableTargetsAreIgnoredAsync(bool onType)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", "class C { object M() => new object(); }");
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var target = onType ? (SyntaxNode)method.ReturnType : method;
        var diagnostic = Diagnostic.Create(ApiSelectionRules.UseConcreteType, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1415UseConcreteTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }
}
