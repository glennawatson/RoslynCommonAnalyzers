// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests brace fixes across else-if chains and stale diagnostics.</summary>
public class Sst1520ConsistentBracesCodeFixProviderTests
{
    /// <summary>Verifies every bare clause is wrapped and an already braced chain preserves its document.</summary>
    /// <param name="body">The original chain.</param>
    /// <param name="expectedBody">The chain after wrapping.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (a) { } else if (b) M(a, b); else M(a, b);", "if (a) { } else if (b) { M(a, b); } else { M(a, b); }")]
    [Arguments("if (a) { } else if (b) { }", "if (a) { } else if (b) { }")]
    public async Task ChainWrappingPreservesExistingBlocksAsync(string body, string expectedBody)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ void M(bool a, bool b) {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var statement = root.DescendantNodes().OfType<IfStatementSyntax>().First();
        var changed = await Sst1520ConsistentBracesCodeFixProvider.WrapChainAsync(document, statement, CancellationToken.None);
        var expected = await CSharpSyntaxTree.ParseText($"class C {{ void M(bool a, bool b) {{ {expectedBody} }} }}").GetRootAsync();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expected.NormalizeWhitespace().ToFullString());
        if (body != expectedBody)
        {
            return;
        }

        await Assert.That(changed).IsSameReferenceAs(document);
    }

    /// <summary>Verifies a diagnostic outside an if statement cannot register or batch brace changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnrelatedDiagnosticHasNoBraceChangesAsync()
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst1520ConsistentBracesCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", "class C { }");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(new("SST1520", "Test", "Test", "Tests", DiagnosticSeverity.Warning, true), root.GetFirstToken().GetLocation());
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }
}
