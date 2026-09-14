// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests volatile wrappers and unsupported diagnostic locations.</summary>
public class Psh1307VolatileInterlockedFieldCodeFixProviderTests
{
    /// <summary>The field identified by the diagnostic.</summary>
    private const string FieldName = "field";

    /// <summary>Checks unsupported writes and stale locations register no action and leave batch edits unchanged.</summary>
    /// <param name="expression">The expression carrying the diagnostic.</param>
    /// <param name="wholeExpression">Whether to diagnose the entire expression instead of the field name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("field += 1", false)]
    [Arguments("++field", false)]
    [Arguments("field--", false)]
    [Arguments("42", true)]
    [Arguments("field + 1", true)]
    public async Task UnsupportedAccessIsUnchangedAsync(string expression, bool wholeExpression)
    {
        var source = $"class C {{ int field; int M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var body = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var node = wholeExpression ? body : body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Single();
        var diagnostic = Diagnostic.Create(ConcurrencyRules.VolatileInterlockedField, node.GetLocation(), FieldName);
        using var container = new ContainerConfiguration().WithPart<Psh1307VolatileInterlockedFieldCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1307VolatileInterlockedFieldCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
        var stale = Diagnostic.Create(ConcurrencyRules.VolatileInterlockedField, root.GetLocation(), FieldName);
        await provider.RegisterCodeFixesAsync(new(document, stale, (action, _) => actions.Add(action), CancellationToken.None));
        BatchEditRegistration.Register<Psh1307VolatileInterlockedFieldCodeFixProvider>(editor, stale);
        await Assert.That(actions).IsEmpty();
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Checks the wrapper uses the available type spelling and preserves assignment roles.</summary>
    /// <param name="preamble">Imports or declarations affecting Volatile lookup.</param>
    /// <param name="expression">The field access or assignment to rewrite.</param>
    /// <param name="replacement">The expected rewritten expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System.Threading;", "field", "Volatile.Read(ref field)")]
    [Arguments("", "field", "global::System.Threading.Volatile.Read(ref field)")]
    [Arguments("class Volatile { }", "field", "global::System.Threading.Volatile.Read(ref field)")]
    [Arguments("namespace Volatile { }", "field", "global::System.Threading.Volatile.Read(ref field)")]
    [Arguments("using System.Threading;", "this.field = 1", "Volatile.Write(ref this.field, 1)")]
    [Arguments("using System.Threading;", "other = field", "other = Volatile.Read(ref field)")]
    public async Task ValidAccessUsesResolvedVolatileSpellingAsync(string preamble, string expression, string replacement)
    {
        var source = $"{preamble} class C {{ int field; int other; int M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var name = root.DescendantNodes().OfType<IdentifierNameSyntax>().Single(static name => name.Identifier.ValueText == FieldName);
        var usage = name.Parent is MemberAccessExpressionSyntax access ? (ExpressionSyntax)access : name;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.VolatileInterlockedField, usage.GetLocation(), FieldName);
        using var container = new ContainerConfiguration().WithPart<Psh1307VolatileInterlockedFieldCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1307VolatileInterlockedFieldCodeFixProvider>(editor, diagnostic);
        var changedExpression = editor.GetChangedRoot().DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        await Assert.That(changedExpression.NormalizeWhitespace().ToFullString()).IsEqualTo(replacement);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression.NormalizeWhitespace().ToFullString()).IsEqualTo(replacement);
    }
}
