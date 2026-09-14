// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests guard edits for stale diagnostics and condition shapes requiring fallback negation.</summary>
public class Sst2273PreferGuardClauseCodeFixProviderTests
{
    /// <summary>Verifies registration and batch editing reject stale guards and directive boundaries.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic source text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("return;", "return")]
    [Arguments("while (flag) if (flag) Work();", "if")]
    [Arguments("if (flag) Work(); else Work();", "if")]
    [Arguments("if (flag) Work(); Work();", "if")]
    [Arguments("if (flag) {\n#region keep\nWork();\n#endregion\n}", "if")]
    public async Task InvalidGuardRegistersNoActionOrBatchEditAsync(string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleGuard", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", $"class C {{ void M(bool flag) {{ {body} }} void Work() {{}} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2273", target);
        using var container = new ContainerConfiguration().WithPart<Sst2273PreferGuardClauseCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2273PreferGuardClauseCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Verifies negation preserves logical meaning and lifts an embedded statement.</summary>
    /// <param name="condition">The wrapping condition.</param>
    /// <param name="expected">The expected negated condition.</param>
    /// <param name="version">The source language version.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value is string", "!(value is string)", LanguageVersion.CSharp8)]
    [Arguments("value is string", "value is not string", LanguageVersion.CSharp9)]
    [Arguments("value is null", "value is not null", LanguageVersion.CSharp9)]
    [Arguments("value is not null", "value is null", LanguageVersion.CSharp9)]
    [Arguments("value is string text", "!(value is string text)", LanguageVersion.CSharp9)]
    [Arguments("flag & other", "!(flag & other)", LanguageVersion.CSharp9)]
    [Arguments("flag | other", "!(flag | other)", LanguageVersion.CSharp9)]
    [Arguments("flag ^ other", "!(flag ^ other)", LanguageVersion.CSharp9)]
    [Arguments("count < floating", "!(count < floating)", LanguageVersion.CSharp9)]
    [Arguments("count >= 1", "count < 1", LanguageVersion.CSharp9)]
    [Arguments("count == 1", "count != 1", LanguageVersion.CSharp9)]
    [Arguments("count != 1", "count == 1", LanguageVersion.CSharp9)]
    [Arguments("!(flag)", "flag", LanguageVersion.CSharp9)]
    [Arguments("(flag && other) || flag", "(!flag || !other) && !flag", LanguageVersion.CSharp9)]
    public async Task NegationBuildsExpectedGuardAsync(string condition, string expected, LanguageVersion version)
    {
        using var workspace = new AdhocWorkspace();
        var source = $"class C {{ void M(object value, bool flag, bool other, int count, double floating) {{ if ({condition}) Work(); }} void Work() {{}} }}";
        var document = workspace.AddProject("GuardNegation", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(version)).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2273", "if");
        using var container = new ContainerConfiguration().WithPart<Sst2273PreferGuardClauseCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var expectedRoot = SyntaxFactory.ParseCompilationUnit(
            $$"""
            class C
            {
                void M(object value, bool flag, bool other, int count, double floating)
                {
                    if ({{expected}}) { return; }
                    Work();
                }
                void Work() {}
            }
            """,
            options: new(version));
        await Assert.That(SyntaxFactory.AreEquivalent(changedRoot, expectedRoot)).IsTrue();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2273PreferGuardClauseCodeFixProvider>(editor, diagnostic);
        await Assert.That(SyntaxFactory.AreEquivalent(editor.GetChangedRoot(), expectedRoot)).IsTrue();
    }
}
