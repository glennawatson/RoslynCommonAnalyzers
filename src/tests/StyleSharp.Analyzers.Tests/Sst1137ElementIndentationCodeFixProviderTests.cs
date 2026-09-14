// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests indentation fix registration and preservation of line content.</summary>
public sealed class Sst1137ElementIndentationCodeFixProviderTests
{
    /// <summary>The source document receiving an indentation fix.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>Checks missing metadata, stale locations and multiline string values prevent an indentation edit.</summary>
    /// <param name="source">The source at the diagnostic location.</param>
    /// <param name="reference">The diagnostic's reference column, or null when absent.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int Value; }", null)]
    [Arguments("class C { int Value; }", "invalid")]
    [Arguments("class C { int Value; }", "2147483648")]
    [Arguments("using System;", "4")]
    [Arguments("class C { string Value = @\"first\nsecond\"; }", "4")]
    [Arguments("class C { string Value = \"\"\"\nfirst\nsecond\n\"\"\"; }", "4")]
    [Arguments("class C { string Value = $@\"first\nsecond\"; }", "4")]
    public async Task UnusableDiagnosticOrMultilineLiteralHasNoActionAsync(string source, string? reference)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = (SyntaxNode?)root.DescendantNodes().OfType<FieldDeclarationSyntax>().FirstOrDefault() ?? root;
        var properties = reference is null
            ? ImmutableDictionary<string, string?>.Empty
            : ImmutableDictionary<string, string?>.Empty.Add(Sst1137ElementIndentationAnalyzer.ReferenceIndentProperty, reference);
        var diagnostic = Diagnostic.Create(ReadabilityRules.ElementsConsistentIndentation, target.GetFirstToken().GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1137ElementIndentationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Checks shifts preserve blank lines, unindented content and single-line string values.</summary>
    /// <param name="source">The member before shifting.</param>
    /// <param name="reference">The target indentation column.</param>
    /// <param name="expected">The exact text after shifting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C\n{\n    int M() => 1;\n}", "4", "class C\n{\n    int M() => 1;\n}")]
    [Arguments("class C\n{\n  int M()\n  {\n \t\n    return 1;\n  }\n}", "4", "class C\n{\n    int M()\n    {\n \t\n      return 1;\n    }\n}")]
    [Arguments("class C\n{\n    int M()\n    {\n\nreturn 1;\n    }\n}", "2", "class C\n{\n  int M()\n  {\n\nreturn 1;\n  }\n}")]
    [Arguments("class C\n{\n  string M() => @\"value\";\n}", "4", "class C\n{\n    string M() => @\"value\";\n}")]
    [Arguments("class C\n{\n  string M() => \"\"\"value\"\"\";\n}", "4", "class C\n{\n    string M() => \"\"\"value\"\"\";\n}")]
    [Arguments("class C\n{\n  string M() => $\"value{1}\";\n}", "4", "class C\n{\n    string M() => $\"value{1}\";\n}")]
    public async Task ShiftPreservesRelativeIndentationAndStringTextAsync(string source, string reference, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var properties = ImmutableDictionary<string, string?>.Empty.Add(Sst1137ElementIndentationAnalyzer.ReferenceIndentProperty, reference);
        var diagnostic = Diagnostic.Create(ReadabilityRules.ElementsConsistentIndentation, method.GetFirstToken().GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1137ElementIndentationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution.GetDocument(document.Id) ?? document;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks a statement diagnostic shifts that statement without moving its enclosing method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StatementShiftLeavesEnclosingMethodInPlaceAsync()
    {
        const string Source = "class C\n{\n    int M()\n    {\n      return 1;\n    }\n}";
        const string Expected = "class C\n{\n    int M()\n    {\n        return 1;\n    }\n}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var statement = root.DescendantNodes().OfType<ReturnStatementSyntax>().Single();
        var properties = ImmutableDictionary<string, string?>.Empty.Add(Sst1137ElementIndentationAnalyzer.ReferenceIndentProperty, "8");
        var diagnostic = Diagnostic.Create(ReadabilityRules.ElementsConsistentIndentation, statement.ReturnKeyword.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst1137ElementIndentationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(Expected);
    }
}
