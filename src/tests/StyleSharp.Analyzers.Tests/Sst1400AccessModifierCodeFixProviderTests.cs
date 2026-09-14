// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests access-modifier edits and stale diagnostic properties.</summary>
public class Sst1400AccessModifierCodeFixProviderTests
{
    /// <summary>Verifies missing modifiers and diagnostics outside members produce no edit.</summary>
    /// <param name="modifier">The modifier property, or null to omit it.</param>
    /// <param name="onMember">Whether the diagnostic points to a member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(null, true)]
    [Arguments("", true)]
    [Arguments("internal", false)]
    public async Task StaleDiagnosticsAreIgnoredAsync(string? modifier, bool onMember)
    {
        const string Source = "using System; class C { }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = onMember ? (SyntaxNode)root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single() : root.DescendantNodes().OfType<UsingDirectiveSyntax>().Single();
        var properties = modifier is null ? ImmutableDictionary<string, string?>.Empty : ImmutableDictionary<string, string?>.Empty.Add(Sst1400AccessModifierAnalyzer.ModifierKey, modifier);
        var diagnostic = Diagnostic.Create(MaintainabilityRules.AccessModifierDeclared, target.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1400AccessModifierCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1400AccessModifierCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies the helper adds the requested accessibility to the member.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AddAsyncDeclaresInternalAccessibilityAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", "class C { }");
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var changed = await Sst1400AccessModifierCodeFixProvider.AddAsync(document, root, member, Accessibility.Internal);
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo("internal class C\r\n{\r\n}");
    }
}
