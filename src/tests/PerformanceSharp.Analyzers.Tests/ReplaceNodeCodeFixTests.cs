// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the boundary between lightbulb registration and replacement construction.</summary>
public class ReplaceNodeCodeFixTests
{
    /// <summary>The document before the action is applied.</summary>
    private const string OriginalSource = "class C { }";

    /// <summary>Verifies every deferred overload waits until invocation before deriving an edit.</summary>
    /// <param name="overload">The registration overload: fixed title, diagnostic title, or semantic.</param>
    /// <param name="applicable">Whether the guard accepts the diagnostic.</param>
    /// <param name="producesEdit">Whether the callback still produces an edit.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(0, true, true)]
    [Arguments(1, true, true)]
    [Arguments(2, true, true)]
    [Arguments(0, false, true)]
    [Arguments(1, false, true)]
    [Arguments(2, false, true)]
    [Arguments(0, true, false)]
    [Arguments(1, true, false)]
    [Arguments(2, true, false)]
    public async Task RegistrationDefersRewriteAsync(int overload, bool applicable, bool producesEdit, CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CodeFixProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(OriginalSource));
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var descriptor = new DiagnosticDescriptor("TEST001", "Test", "Test", "Test", DiagnosticSeverity.Warning, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, root.GetLocation());
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
        var rewrites = 0;
        Diagnostic? resolvedDiagnostic = null;

        NodeReplacement? Rewrite(SyntaxNode original, Diagnostic reported)
        {
            rewrites++;
            resolvedDiagnostic = reported;
            return producesEdit ? new NodeReplacement(original, SyntaxFactory.ParseCompilationUnit("class D { }")) : null;
        }

        switch (overload)
        {
            case 0:
            {
                await ReplaceNodeCodeFix.RegisterAsync(context, "Fix", "Key", (_, _) => applicable, Rewrite);
                break;
            }

            case 1:
            {
                await ReplaceNodeCodeFix.RegisterAsync(context, static d => d.Id, static d => d.Id, (_, _) => applicable, Rewrite);
                break;
            }

            default:
            {
                await ReplaceNodeCodeFix.RegisterAsync(context, "Fix", "Key", (_, _, _) => applicable, (original, _, reported) => Rewrite(original, reported));
                break;
            }
        }

        await Assert.That(rewrites).IsEqualTo(0);
        await Assert.That(actions.Count).IsEqualTo(applicable ? 1 : 0);
        if (!applicable)
        {
            return;
        }

        await Assert.That(actions[0].Title).IsEqualTo(overload == 1 ? diagnostic.Id : "Fix");
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo(overload == 1 ? diagnostic.Id : "Key");
        var text = await ApplyAsync(document, actions[0], cancellationToken);
        await Assert.That(rewrites).IsEqualTo(1);
        await Assert.That(resolvedDiagnostic).IsEqualTo(diagnostic);
        await Assert.That(text).IsEqualTo(producesEdit ? "class D { }" : OriginalSource);
    }

    /// <summary>Verifies callbacks retain their own diagnostic when several actions are registered.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ActionsRetainTheirDiagnosticAsync(CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CodeFixProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(OriginalSource));
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var first = Diagnostic.Create(new("TEST001", "Test", "Test", "Test", DiagnosticSeverity.Warning, true), root.GetLocation());
        var second = Diagnostic.Create(new("TEST002", "Test", "Test", "Test", DiagnosticSeverity.Warning, true), root.GetLocation());
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, root.Span, [first, second], (action, _) => actions.Add(action), cancellationToken);
        await ReplaceNodeCodeFix.RegisterAsync(
            context,
            static d => d.Id,
            static d => d.Id,
            static (_, _) => true,
            static (original, diagnostic) => new NodeReplacement(original, SyntaxFactory.ParseCompilationUnit($"class {diagnostic.Id} {{ }}")));

        await Assert.That(actions.Count).IsEqualTo(context.Diagnostics.Length);
        for (var index = 0; index < actions.Count; index++)
        {
            var text = await ApplyAsync(document, actions[index], cancellationToken);
            await Assert.That(text).IsEqualTo($"class {actions[index].Title} {{ }}");
        }
    }

    /// <summary>Invokes an action and reads the resulting document without changing the workspace.</summary>
    /// <param name="document">The original document.</param>
    /// <param name="action">The registered action.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The resulting source text.</returns>
    private static async Task<string> ApplyAsync(Document document, CodeAction action, CancellationToken cancellationToken)
    {
        var operations = await action.GetOperationsAsync(cancellationToken);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var text = await changed.GetTextAsync(cancellationToken);
        return text.ToString();
    }
}
