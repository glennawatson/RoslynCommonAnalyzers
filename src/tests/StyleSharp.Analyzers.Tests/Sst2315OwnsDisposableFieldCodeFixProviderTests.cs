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

/// <summary>Tests disposable ownership fix metadata and multiple owned members.</summary>
public class Sst2315OwnsDisposableFieldCodeFixProviderTests
{
    /// <summary>The original base class and the added disposal interface.</summary>
    private const int ExpectedBaseTypeCount = 2;

    /// <summary>Checks missing member metadata and locations outside a type are ignored.</summary>
    /// <param name="source">The source receiving the diagnostic.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <param name="members">The members metadata, or null to omit it.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }", "C", null)]
    [Arguments("class C { }", "C", "")]
    [Arguments("using System;", "System", "field")]
    [Arguments("class C {\n#region Keep\nobject field;\n#endregion\n}", "C", "field")]
    public async Task InapplicableDiagnosticDoesNotRegisterOrEditAsync(string source, string target, string? members)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("DisposableMetadata", LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var original = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2315", target);
        var properties = members is null ? ImmutableDictionary<string, string?>.Empty : ImmutableDictionary<string, string?>.Empty.Add(Sst2315OwnsDisposableFieldAnalyzer.MembersToDisposeKey, members);
        var diagnostic = Diagnostic.Create(original.Descriptor, original.Location, properties);
        using var container = new ContainerConfiguration().WithPart<Sst2315OwnsDisposableFieldCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Checks every owned member is disposed in metadata order in single and batch edits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultipleOwnedMembersAreDisposedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("MultipleDisposables", LanguageNames.CSharp).AddDocument("Test.cs", "class C : B { object first, second, third; }");
        var root = (await document.GetSyntaxRootAsync())!;
        var original = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2315", "C");
        var properties = ImmutableDictionary<string, string?>.Empty.Add(Sst2315OwnsDisposableFieldAnalyzer.MembersToDisposeKey, "first,second,third");
        var diagnostic = Diagnostic.Create(original.Descriptor, original.Location, properties);
        using var container = new ContainerConfiguration().WithPart<Sst2315OwnsDisposableFieldCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var method = changedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        await Assert.That(method.Body!.Statements.Select(static statement => statement.ToString())).IsEquivalentTo(["first.Dispose();", "second.Dispose();", "third.Dispose();"]);
        await Assert.That(changedRoot.DescendantNodes().OfType<BaseListSyntax>().Single().Types.Count).IsEqualTo(ExpectedBaseTypeCount);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        var batchMethod = editor.GetChangedRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        await Assert.That(batchMethod.Body!.Statements.Select(static statement => statement.ToString())).IsEquivalentTo(["first.Dispose();", "second.Dispose();", "third.Dispose();"]);
    }
}
