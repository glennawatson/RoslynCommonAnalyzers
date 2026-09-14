// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests using containers and code-fix applicability.</summary>
public class UsingSortCodeFixProviderTests
{
    /// <summary>The test document name.</summary>
    private const string TestFileName = "Test.cs";

    /// <summary>Checks a large reversed import list is completely sorted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LargeUsingListIsSortedAsync()
    {
        const string Source = """
            using Z; using Y; using X; using W; using V; using U; using T; using S; using R; using Q; using P; using O; using N;
            using M; using L; using K; using J; using I; using H; using G; using F; using E; using D; using C; using B; using A;
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ManyUsings", LanguageNames.CSharp).AddDocument(TestFileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var changed = await UsingSortCodeFixProvider.SortAsync(document, root, CancellationToken.None);
        var changedRoot = (CompilationUnitSyntax)(await changed.GetSyntaxRootAsync())!;
        await Assert.That(string.Join(",", changedRoot.Usings.Select(static directive => directive.Name!.ToString()))).IsEqualTo("A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z");
    }

    /// <summary>Checks each supported container is sorted while comments remain at their positions.</summary>
    /// <param name="source">The unsorted source.</param>
    /// <param name="expected">The sorted source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System.Text;\nusing System;\n", "using System;\nusing System.Text;\n")]
    [Arguments("namespace N { using System.Text; using System; }", "namespace N { using System; using System.Text; }")]
    [Arguments("namespace N;\nusing System.Text;\nusing System;\n", "namespace N;\nusing System;\nusing System.Text;\n")]
    [Arguments("// first\nglobal using System.Text;\n// second\nglobal using System;\n", "// first\nglobal using System;\n// second\nglobal using System.Text;\n")]
    [Arguments("using System;\nusing System.Text;\nusing System.Collections;\n", "using System;\nusing System.Collections;\nusing System.Text;\n")]
    public async Task SortPreservesContainerAndSlotTriviaAsync(string source, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("UsingSort", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var directive = root.DescendantNodes().OfType<UsingDirectiveSyntax>().First();
        var changed = await UsingSortCodeFixProvider.SortAsync(document, directive.Parent!, CancellationToken.None);
        await Assert.That((await changed.GetSyntaxRootAsync())!.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Checks unsupported and trivial containers retain the original document.</summary>
    /// <param name="source">The source with zero or one using.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C {}")]
    [Arguments("using System; class C {}")]
    public async Task TrivialContainerIsUnchangedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("TrivialUsings", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        await Assert.That(await UsingSortCodeFixProvider.SortAsync(document, root, CancellationToken.None)).IsSameReferenceAs(document);
        var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        await Assert.That(await UsingSortCodeFixProvider.SortAsync(document, declaration, CancellationToken.None)).IsSameReferenceAs(document);
    }

    /// <summary>Checks stale locations and directives prevent registration.</summary>
    /// <param name="source">The source presented to the code fix.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C {}", "C")]
    [Arguments("using System.Text;\n#if true\nusing System;\n#endif\n", "using System;")]
    [Arguments("using System.Text;\n#region Keep\nusing System;\n#endregion\n", "using System;")]
    public async Task StaleOrDirectiveSpanningUsingsHaveNoActionAsync(string source, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("UsingRegistration", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST1208", target);
        using var container = new ContainerConfiguration().WithPart<UsingSortCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }
}
