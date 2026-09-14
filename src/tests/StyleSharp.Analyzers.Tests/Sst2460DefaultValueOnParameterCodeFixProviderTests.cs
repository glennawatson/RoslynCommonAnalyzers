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

/// <summary>Tests parameter-default fix applicability after the reported attribute changes.</summary>
public sealed class Sst2460DefaultValueOnParameterCodeFixProviderTests
{
    /// <summary>Checks unsafe or stale defaults are rejected by individual and batch fixes.</summary>
    /// <param name="source">The source at the stale diagnostic.</param>
    /// <param name="hasInteropAttribute">Whether the framework supplies the replacement attribute.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M(int value) { } }", true)]
    [Arguments("class C { void M([DefaultValue] int value) { } }", true)]
    [Arguments("class C { void M([DefaultValue()] int value) { } }", true)]
    [Arguments("class C { void M([DefaultValue(typeof(int), \"1\")] int value) { } }", true)]
    [Arguments("class C { void M([DefaultValue(Value = 1)] int value) { } }", true)]
    [Arguments("class C { void M([DefaultValue(value: 1)] int value) { } }", true)]
    [Arguments("class C { void M([DefaultValue(1)] int value) { } }", false)]
    [Arguments("class C { [DefaultValue(1)] int Value { get; } }", true)]
    [Arguments("class C { void M([DefaultValue(\"one\")] int value) { } }", true)]
    [Arguments("class C { void M<T>([DefaultValue(1)] T value) { } }", true)]
    [Arguments("class C { void M([DefaultValue(null)] int value) { } }", true)]
    public async Task InapplicableDefaultDoesNotRegisterOrRewriteAsync(string source, bool hasInteropAttribute)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        if (hasInteropAttribute)
        {
            project = project.WithMetadataReferences(RuntimeMetadataReferences.Platform);
        }

        var document = project.AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = (SyntaxNode?)root.DescendantNodes().OfType<AttributeSyntax>().FirstOrDefault() ?? root;
        var diagnostic = Diagnostic.Create(CorrectnessRules.DefaultValueOnParameter, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2460DefaultValueOnParameterCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2460DefaultValueOnParameterCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Checks qualified spellings retain their suffix while a stale generic name uses the short replacement.</summary>
    /// <param name="attributeName">The current attribute spelling.</param>
    /// <param name="replacementName">The replacement spelling.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.ComponentModel.DefaultValueAttribute", "DefaultParameterValueAttribute")]
    [Arguments("DefaultAlias::DefaultValue", "DefaultParameterValue")]
    [Arguments("DefaultAlias::DefaultValueAttribute", "DefaultParameterValueAttribute")]
    [Arguments("DefaultValue<int>", "DefaultParameterValue")]
    public async Task QualifiedAndGenericAttributeNamesAreRewrittenAsync(string attributeName, string replacementName)
    {
        var source = $"using DefaultAlias = System.ComponentModel; using System.Runtime.InteropServices; class C {{ void M([{attributeName}(1)] int value) {{ }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var attribute = root.DescendantNodes().OfType<AttributeSyntax>().Single();
        var diagnostic = Diagnostic.Create(CorrectnessRules.DefaultValueOnParameter, attribute.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2460DefaultValueOnParameterCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2460DefaultValueOnParameterCodeFixProvider>(editor, diagnostic);
        var expected = $"using DefaultAlias = System.ComponentModel; using System.Runtime.InteropServices; class C {{ void M([{replacementName}(1)] int value) {{ }} }}";
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }
}
