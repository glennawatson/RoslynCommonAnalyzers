// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests Clear fallback overloads and speculative binding failures.</summary>
public class Psh1127ClearOverFillDefaultCodeFixProviderTests
{
    /// <summary>Verifies unresolved framework types and shadowed Clear methods do not produce an edit.</summary>
    /// <param name="source">The source containing a single Fill call.</param>
    /// <param name="hasFramework">Whether runtime references are available.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M(int[] values) { Array.Fill(values, 0); } }", false)]
    [Arguments("using static System.Array; class C { void Clear(System.Array values) { } void M(int[] values) { Fill(values, 0); } }", true)]
    [Arguments("using static System.Array; class C { static void Clear(System.Array values) { } void M(int[] values) { Fill(values, 0); } }", true)]
    public async Task MissingOrShadowedFrameworkMethodIsRejectedAsync(string source, bool hasFramework)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        if (hasFramework)
        {
            project = project.WithMetadataReferences(RuntimeMetadataReferences.Platform);
        }

        var document = project.AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(CollectionRules.ClearOverFillDefault, invocation.GetLocation(), "Array.Clear");
        using var container = new ContainerConfiguration().WithPart<Psh1127ClearOverFillDefaultCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(Psh1127ClearOverFillDefaultCodeFixProvider.Apply(document, root, model, invocation)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies missing or incompatible Clear overloads reject edits and legacy overloads preserve evaluation.</summary>
    /// <param name="clear">The available Clear declaration.</param>
    /// <param name="expression">The original expression.</param>
    /// <param name="replacement">The expected expression, or null for rejection.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static void Clear(int[] value, int start, int count) { }", "Array.Fill(values, 0)", "Array.Clear(values, 0, values.Length)")]
    [Arguments("public static void Clear(int[] value, int start, int count) { }", "Array.Fill(Get(), 0)", null)]
    [Arguments("public static void Clear(int[] value, int start, int count) { }", "Array.Fill(values, 0, 1, 2)", "Array.Clear(values, 1, 2)")]
    [Arguments("public static void Clear(int[] value) { }", "Fill(values, 0)", "Clear(values)")]
    [Arguments("public static void Clear(int[] value) { }", "Array.Fill(Get(), 0)", "Array.Clear(Get())")]
    [Arguments("public static void Clear(string value) { }", "Array.Fill(values, 0)", null)]
    [Arguments("public void Clear(int[] value) { }", "Array.Fill(values, 0)", null)]
    [Arguments("", "Array.Fill(values, 0)", null)]
    [Arguments("public static void Clear(int[] value) { }", "Array.Fill(values, 1)", null)]
    [Arguments("public static void Clear(int[] value) { }", "42", null)]
    public async Task AvailableClearOverloadControlsEditAsync(string clear, string expression, string? replacement)
    {
        var source = $$"""
            using System;
            using static System.Array;
            namespace System
            {
                public class Array
                {
                    public static void Fill(int[] value, int fill) { }
                    {{clear}}
                }
            }
            class C
            {
                int[] Get() => null;
                object M(int[] values) => {{expression}};
            }
            """;
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var node = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "M").ExpressionBody!.Expression;
        var diagnostic = Diagnostic.Create(CollectionRules.ClearOverFillDefault, node.GetLocation(), "Array.Clear");
        using var container = new ContainerConfiguration().WithPart<Psh1127ClearOverFillDefaultCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(replacement is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        var expected = SyntaxFactory.ParseExpression(replacement ?? expression).NormalizeWhitespace().ToFullString();
        var changedNode = editor.GetChangedRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "M").ExpressionBody!.Expression;
        await Assert.That(changedNode.NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var changed = Psh1127ClearOverFillDefaultCodeFixProvider.Apply(document, root, model, invocation);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(editor.GetChangedRoot().ToFullString());
    }
}
