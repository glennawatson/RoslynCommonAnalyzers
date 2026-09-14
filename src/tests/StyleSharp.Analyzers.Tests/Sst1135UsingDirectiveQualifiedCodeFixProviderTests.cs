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

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests qualification refusal for stale syntax and unresolved or non-type symbols.</summary>
public class Sst1135UsingDirectiveQualifiedCodeFixProviderTests
{
    /// <summary>Verifies single and batch fixes reject targets that no longer name a namespace or type.</summary>
    /// <param name="source">The source containing the target.</param>
    /// <param name="targetKind">The target syntax shape.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using Missing;", "name")]
    [Arguments("using System;", "directive")]
    [Arguments("class C { int value; int M() => value; }", "value")]
    public async Task InvalidTargetsAreIgnoredAsync(string source, string targetKind)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = targetKind switch
        {
            "directive" => (SyntaxNode)root.DescendantNodes().OfType<UsingDirectiveSyntax>().Single(),
            "value" => root.DescendantNodes().OfType<IdentifierNameSyntax>().Single(),
            _ => root.DescendantNodes().OfType<UsingDirectiveSyntax>().Single().Name!,
        };
        var diagnostic = Diagnostic.Create(ReadabilityRules.UsingDirectiveQualified, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1135UsingDirectiveQualifiedCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        await ((IAsyncBatchableCodeFix)provider).RegisterEditsAsync(editor, diagnostic, CancellationToken.None);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
