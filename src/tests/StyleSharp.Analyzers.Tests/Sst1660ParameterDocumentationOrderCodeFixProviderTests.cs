// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests parameter-documentation fixes when the documented signature has changed.</summary>
public class Sst1660ParameterDocumentationOrderCodeFixProviderTests
{
    /// <summary>Checks incomplete, unnamed, mismatched, and already ordered documentation is left alone.</summary>
    /// <param name="source">The current document.</param>
    /// <param name="target">The stale diagnostic text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() {} }", "M")]
    [Arguments("/// <param name=\"a\"/>\n", "<param name=\"a\"/>")]
    [Arguments("class C {\n/// <param name=\"a\"/>\npublic void M(int a) {} }", "<param name=\"a\"/>")]
    [Arguments("class C {\n/// <param name=\"a\"/>\npublic void M(int a, int b) {} }", "<param name=\"a\"/>")]
    [Arguments("class C {\n/// <param/><param name=\"a\"/>\npublic void M(int a, int b) {} }", "<param/>")]
    [Arguments("class C {\n/// <param>Text</param><param name=\"a\"/>\npublic void M(int a, int b) {} }", "<param>Text</param>")]
    [Arguments("class C {\n/// <param name=\"z\"/><param name=\"b\"/>\npublic void M(int a, int b) {} }", "<param name=\"z\"/>")]
    [Arguments("class C {\n/// <param name=\"a\"/><param name=\"b\"/>\npublic void M(int a, int b) {} }", "<param name=\"a\"/>")]
    public async Task InapplicableDocumentationDoesNotRegisterOrEditAsync(string source, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ParameterOrder", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose))
            .AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST1660", target);
        using var container = new ContainerConfiguration().WithPart<Sst1660ParameterDocumentationOrderCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changes = new List<TextChange>();
        Sst1660ParameterDocumentationOrderCodeFixProvider.BuildChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Checks matching elements remain in their slots while later empty elements are reordered.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MatchingPrefixAndSummaryArePreservedAsync()
    {
        const string Source = "class C {\n/// <summary>Keep.</summary>\n/// <param name=\"a\"/>\n/// <param name=\"c\"/>\n/// <param name=\"b\"/>\npublic void M(int a, int b, int c) {} }";
        const string Expected = "class C {\n/// <summary>Keep.</summary>\n/// <param name=\"a\"/>\n/// <param name=\"b\"/>\n/// <param name=\"c\"/>\npublic void M(int a, int b, int c) {} }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ParameterOrder", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose))
            .AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST1660", "<param name=\"c\"/>");
        using var container = new ContainerConfiguration().WithPart<Sst1660ParameterDocumentationOrderCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(Expected);
        var text = await document.GetTextAsync();
        var changes = new List<TextChange>();
        Sst1660ParameterDocumentationOrderCodeFixProvider.BuildChanges(text, root, diagnostic, changes);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(Expected);
    }

    /// <summary>Records that a method beginning with its return type is reported but currently has no reordering fix.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnmodifiedMethodReportsWithoutFixAsync()
    {
        const string Source = """
            class C
            {
                /// {|SST1660:<param name="b"/>|}
                /// <param name="a"/>
                void M(int a, int b) { }
            }
            """;
        await CSharpCodeFixVerifier<Sst1660ParameterDocumentationOrderAnalyzer, Sst1660ParameterDocumentationOrderCodeFixProvider>.VerifyCodeFixAsync(Source, Source);
    }
}
