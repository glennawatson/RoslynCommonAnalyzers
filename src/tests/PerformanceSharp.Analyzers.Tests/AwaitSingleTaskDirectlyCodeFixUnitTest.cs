// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1301AwaitSingleTaskDirectlyAnalyzer,
    PerformanceSharp.Analyzers.Psh1301AwaitSingleTaskDirectlyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests single-task rewrites, expression precedence, and stale diagnostics.</summary>
public class AwaitSingleTaskDirectlyCodeFixUnitTest
{
    /// <summary>Verifies both combinators preserve the argument's precedence and surrounding trivia.</summary>
    /// <param name="argument">The single task expression.</param>
    /// <param name="replacement">The expression after any required parentheses.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("task", "task")]
    [Arguments("Task.CompletedTask", "Task.CompletedTask")]
    [Arguments("GetTask()", "GetTask()")]
    [Arguments("tasks[0]", "tasks[0]")]
    [Arguments("(task)", "(task)")]
    [Arguments("flag ? task : Task.CompletedTask", "(flag ? task : Task.CompletedTask)")]
    [Arguments("task ?? Task.CompletedTask", "(task ?? Task.CompletedTask)")]
    [Arguments("(Task)task", "((Task)task)")]
    public async Task SingleTaskPreservesPrecedenceAsync(string argument, string replacement)
    {
        var source = $$"""
            using System.Threading.Tasks;
            class C
            {
                private Task GetTask() => Task.CompletedTask;
                public async Task M(Task task, Task[] tasks, bool flag)
                {
                    await /* before */ {|PSH1301:Task.WhenAll({{argument}})|} /* after */;
                    {|PSH1301:Task.WaitAll({{argument}})|};
                }
            }
            """;
        var fixedSource = $$"""
            using System.Threading.Tasks;
            class C
            {
                private Task GetTask() => Task.CompletedTask;
                public async Task M(Task task, Task[] tasks, bool flag)
                {
                    await /* before */ {{replacement}} /* after */;
                    {{replacement}}.Wait();
                }
            }
            """;
        await Verify.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies rejected locations register no action and leave batch edits unchanged.</summary>
    /// <param name="expression">The stale diagnostic's expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("task")]
    [Arguments("Task.WhenAll()")]
    [Arguments("Task.WhenAll(a, b)")]
    [Arguments("Task.WhenAny(task)")]
    [Arguments("WhenAll(task)")]
    public async Task StaleDiagnosticIsIgnoredAsync(string expression)
    {
        var source = $"class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var node = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(ConcurrencyRules.AwaitSingleTaskDirectly, node.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1301AwaitSingleTaskDirectlyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
