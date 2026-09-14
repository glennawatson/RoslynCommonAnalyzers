// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.CodeFixes;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests direct builder rewrites and rejection of stale inner-call shapes.</summary>
public class Psh1203StringBuilderInnerAllocationCodeFixProviderTests
{
    /// <summary>Verifies the classifier and each edit path agree on nested-call shapes.</summary>
    /// <param name="expression">The reported invocation.</param>
    /// <param name="expected">The replacement invocation, or null when the fix is rejected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("builder.Append()", null)]
    [Arguments("builder.Append(text, 1)", null)]
    [Arguments("builder.Append(text)", null)]
    [Arguments("builder.Append(GetText())", null)]
    [Arguments("builder.Append(value->ToString())", null)]
    [Arguments("builder.Append(value.ToString<int>())", null)]
    [Arguments("builder.Append(value.ToString(\"x\"))", null)]
    [Arguments("builder.Append(text.Substring())", null)]
    [Arguments("builder.Append(text.Substring(1, 2, 3))", null)]
    [Arguments("builder.Append(GetText().Substring(1))", null)]
    [Arguments("builder.Append(text.Trim())", null)]
    [Arguments("builder.Append(value.ToString())", "builder.Append(value)")]
    [Arguments("builder.Append(string.Format(\"{0}\", value))", "builder.AppendFormat(\"{0}\", value)")]
    [Arguments("builder.Append(text.Substring(1, 2))", "builder.Append(text, 1, 2)")]
    [Arguments("builder.Append(text.Substring(value + 1))", "builder.Append(text, value + 1, text.Length - (value + 1))")]
    [Arguments("builder.Append(text.Substring(value))", "builder.Append(text, value, text.Length - value)")]
    [Arguments("builder.Append(text.Substring(text.Length))", "builder.Append(text, text.Length, text.Length - text.Length)")]
    [Arguments("builder.Append(text.Substring(GetIndex()))", "builder.Append(text, GetIndex(), text.Length - GetIndex())")]
    [Arguments("builder.Append(text.Substring(indices[0]))", "builder.Append(text, indices[0], text.Length - indices[0])")]
    [Arguments("builder.Append(text.Substring((value)))", "builder.Append(text, (value), text.Length - (value))")]
    public async Task InnerCallShapeControlsEditsAsync(string expression, string? expected)
    {
        var source = $$"""class C { void M(System.Text.StringBuilder builder, string text, int value, int[] indices) { {{expression}}; } }""";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var invocation = (InvocationExpressionSyntax)root.DescendantNodes().OfType<ExpressionStatementSyntax>().Single().Expression;
        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name;
        var diagnostic = Diagnostic.Create(StringRules.StringBuilderInnerAllocation, name.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1203StringBuilderInnerAllocationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1203StringBuilderInnerAllocationCodeFixProvider>(editor, diagnostic);
        var changed = TargetCodeFix.Apply(document, root, invocation, Psh1203StringBuilderInnerAllocationCodeFixProvider.Rewrite);
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(editor.GetChangedRoot().ToFullString());
        var result = editor.GetChangedRoot().DescendantNodes().OfType<ExpressionStatementSyntax>().Single().Expression;
        await Assert.That(result.NormalizeWhitespace().ToFullString()).IsEqualTo(expected ?? expression);
    }

    /// <summary>Verifies a diagnostic moved to a declaration no longer offers an Append rewrite.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NoninvocationDiagnosticIsIgnoredAsync()
    {
        const string Source = "class C { }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(StringRules.StringBuilderInnerAllocation, root.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1203StringBuilderInnerAllocationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1203StringBuilderInnerAllocationCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }
}
