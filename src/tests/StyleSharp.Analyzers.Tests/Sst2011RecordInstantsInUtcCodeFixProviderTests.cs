// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests UTC-clock fixes against stale syntax and lookalike clock types.</summary>
public class Sst2011RecordInstantsInUtcCodeFixProviderTests
{
    /// <summary>Checks registration and batch edits require a UTC property with the same result type.</summary>
    /// <param name="expression">The original expression.</param>
    /// <param name="members">Members of the lookalike clock.</param>
    /// <param name="expected">The replacement expression, or null when rejected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("1", "", null)]
    [Arguments("DateTime.Other", "public static int Other => 1;", null)]
    [Arguments("DateTime.Now", "public static int Now => 1;", null)]
    [Arguments("DateTime.Now", "public static int Now => 1; public static string UtcNow => null;", null)]
    [Arguments("DateTime.Now", "public static int Now => 1; public static int UtcNow;", null)]
    [Arguments("DateTime.Now", "public static int Now => 1; public static int UtcNow => 2;", "DateTime.UtcNow")]
    [Arguments("DateTime.Today", "public static int Today => 1; public static DateTime UtcNow => null; public int Date => 2;", "DateTime.UtcNow.Date")]
    public async Task ReplacementMustBindToEquivalentPropertyTypeAsync(string expression, string members, string? expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("UtcClock", LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", $"class C {{ object M() => {expression}; }} class DateTime {{ {members} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST2011", expression);
        using var container = new ContainerConfiguration().WithPart<Sst2011RecordInstantsInUtcCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        await Assert.That(Sst2011RecordInstantsInUtcCodeFixProvider.TryBuildReplacement(root, model, diagnostic, out _, out var replacement)).IsEqualTo(expected is not null);
        await Assert.That(replacement?.ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        var expectedSource = $"class C {{ object M() => {expected ?? expression}; }} class DateTime {{ {members} }}";
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expectedSource);
    }
}
