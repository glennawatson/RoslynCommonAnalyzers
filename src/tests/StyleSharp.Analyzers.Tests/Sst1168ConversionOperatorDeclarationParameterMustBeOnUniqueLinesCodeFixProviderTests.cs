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

/// <summary>Tests conversion-parameter edits on incomplete declarations and stale diagnostics.</summary>
public class Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProviderTests
{
    /// <summary>Verifies a supplied diagnostic splits every parameter consistently for single and batch edits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IncompleteConversionParametersAreSplitAsync()
    {
        const string Source = """
            class C
            {
                public static implicit operator int(
                    C first, C second) => 0;
            }
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var conversion = root.DescendantNodes().OfType<ConversionOperatorDeclarationSyntax>().Single();
        var descriptor = new Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer().SupportedDiagnostics.Single();
        var diagnostic = Diagnostic.Create(descriptor, conversion.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1168");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(BatchEditFixAllProvider.Instance);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var parameters = changedRoot.DescendantNodes().OfType<ConversionOperatorDeclarationSyntax>().Single().ParameterList.Parameters;
        await Assert.That(parameters[0].GetLocation().GetLineSpan().StartLinePosition.Line < parameters[1].GetLocation().GetLineSpan().StartLinePosition.Line).IsTrue();
        var direct = await Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider.FixAsync(document, root, conversion);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo((await direct.GetTextAsync()).ToString());
    }

    /// <summary>Verifies diagnostics outside a conversion declaration do not register or edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StaleConversionDiagnosticIsIgnoredAsync()
    {
        const string Source = "class C { }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var descriptor = new Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer().SupportedDiagnostics.Single();
        var diagnostic = Diagnostic.Create(descriptor, root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }
}
