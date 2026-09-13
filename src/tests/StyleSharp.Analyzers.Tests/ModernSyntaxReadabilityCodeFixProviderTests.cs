// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests rejected modern readability edits when diagnostics outlive their original syntax.</summary>
public class ModernSyntaxReadabilityCodeFixProviderTests
{
    /// <summary>Checks registration, single application, and batch editing all reject stale syntax.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("SST2212", "return 1;", "return")]
    [Arguments("SST2212", "return 1;", "1")]
    [Arguments("SST2213", "return x is int;", "int")]
    [Arguments("SST2213", "return x switch { int _ => 1 };", "int _")]
    [Arguments("SST2214", "return x;", "return")]
    [Arguments("SST2214", "int x;", "int x;")]
    [Arguments("SST2214", "int x = 1, y = 2;", "int x = 1, y = 2;")]
    [Arguments("SST2214", "var tuple = F();", "var tuple = F();")]
    [Arguments("SST2214", "switch (x) { case 1: var tuple = F(); var a = tuple.Item1; var b = tuple.Item2; }", "var tuple = F();")]
    [Arguments("SST2214", "var tuple = F(); Log(); var b = tuple.Item2;", "var tuple = F();")]
    [Arguments("SST2214", "var tuple = F(); var a = tuple.Item1; Log();", "var tuple = F();")]
    [Arguments("SST2214", "var tuple = F(); int a = 1, b = 2; var c = tuple.Item2;", "var tuple = F();")]
    [Arguments("SST2214", "var tuple = F(); var a = tuple.Item1; int b = 1, c = 2;", "var tuple = F();")]
    [Arguments("SST2215", "return x;", "return")]
    [Arguments("SST2215", "int temp;", "int temp;")]
    [Arguments("SST2215", "var temp = F();", "var temp = F();")]
    [Arguments("SST2215", "var temp = left;", "var temp = left;")]
    [Arguments("SST2215", "switch (x) { case 1: var temp = left; left = right; right = temp; }", "var temp = left;")]
    [Arguments("SST2215", "var temp = left; int x = 1; right = temp;", "var temp = left;")]
    [Arguments("SST2215", "var temp = left; Log(); right = temp;", "var temp = left;")]
    [Arguments("SST2215", "var temp = left; left = 1; right = temp;", "var temp = left;")]
    [Arguments("SST2215", "var temp = left; left = right; return;", "var temp = left;")]
    [Arguments("SST2216", "return x;", "return")]
    [Arguments("SST2216", "return (a: b, c);", "a: b")]
    [Arguments("SST2216", "return (a, b);", "a")]
    [Arguments("SST2217", "return x;", "return")]
    [Arguments("SST2217", "return x;", "x")]
    [Arguments("SST0000", "return x;", "x")]
    public async Task StaleDiagnosticDoesNotRegisterOrEditAsync(string id, string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleModernReadability", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ object M() {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, id, target);
        using var container = new ContainerConfiguration().WithPart<ModernSyntaxReadabilityCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(0);
        await Assert.That(ModernSyntaxReadabilityCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks missing or unsupported UTF-8 target metadata does not produce an edit.</summary>
    /// <param name="target">The target metadata value.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(null)]
    [Arguments("unsupported")]
    public async Task Utf8FixRejectsInvalidTargetMetadataAsync(string? target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("Utf8Metadata", LanguageNames.CSharp).AddDocument("Test.cs", "class C { object M() => 1; }");
        var root = (await document.GetSyntaxRootAsync())!;
        var original = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2212", "1");
        var diagnostic = Diagnostic.Create(original.Descriptor, original.Location, ImmutableDictionary<string, string?>.Empty.Add(ModernSyntaxReadabilityAnalysis.Utf8TargetKey, target));
        await Assert.That(ModernSyntaxReadabilityCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
    }

    /// <summary>Checks a supplied name diagnostic preserves ref syntax and leading argument comments.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task NameFixPreservesRefArgumentTriviaAsync()
    {
        using var workspace = new AdhocWorkspace();
        const string Source = "class C { void M(ref int value) { Use(/*keep*/value: ref value); } void Use(ref int value) {} }";
        var document = workspace.AddProject("RefArgument", LanguageNames.CSharp).AddDocument("RefArgument.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2216", "value: ref value");
        var changed = ModernSyntaxReadabilityCodeFixProvider.Apply(document, root, diagnostic);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.ToFullString()).Contains("Use(/*keep*/ref value)");
    }
}
