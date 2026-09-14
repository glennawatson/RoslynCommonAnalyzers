// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests static-class edits with missing modifiers and stale diagnostics.</summary>
public class MakeClassStaticCodeFixProviderTests
{
    /// <summary>Verifies both edit entry points retain the current comment placement and modifier ordering.</summary>
    /// <param name="modifiers">The existing modifiers.</param>
    /// <param name="expectedPrefix">The rewritten modifiers and leading comment.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "// Utilities\nstatic ")]
    [Arguments("public ", "// Utilities\npublic static ")]
    [Arguments("partial ", "static // Utilities\npartial ")]
    public async Task StaticModifierRetainsCurrentTriviaPlacementAsync(string modifiers, string expectedPrefix)
    {
        var source = $"// Utilities\n{modifiers}class C {{ public static int Value; }}";
        var expected = $"{expectedPrefix}class C {{ public static int Value; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.MakeClassStatic, declaration.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<MakeClassStaticCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<MakeClassStaticCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a diagnostic whose class was removed cannot register or apply an edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticOutsideClassIsIgnoredAsync()
    {
        const string Source = "struct C { }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(MaintainabilityRules.MakeClassStatic, root.DescendantNodes().OfType<StructDeclarationSyntax>().Single().Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<MakeClassStaticCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<MakeClassStaticCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }
}
