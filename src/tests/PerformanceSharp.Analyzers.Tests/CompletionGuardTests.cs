// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyBlocking = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1315NoBlockingWaitAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the syntax proofs that distinguish completed tasks from potentially blocking waits.</summary>
public class CompletionGuardTests
{
    /// <summary>Verifies completion must follow from the condition along the branch containing the wait.</summary>
    /// <param name="source">A statement containing one result read.</param>
    /// <param name="expected">Whether the condition proves the task complete.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (task.IsCompleted) return task.Result;", true)]
    [Arguments("if (task.IsCompletedSuccessfully) return task.Result;", true)]
    [Arguments("if (((task.IsCompleted))) return task.Result;", true)]
    [Arguments("if (!!task.IsCompleted) return task.Result;", true)]
    [Arguments("if (!task.IsCompleted) return task.Result;", false)]
    [Arguments("if (enabled && task.IsCompleted) return task.Result;", true)]
    [Arguments("if (task.IsCompleted && enabled) return task.Result;", true)]
    [Arguments("if (enabled && other.IsCompleted) return task.Result;", false)]
    [Arguments("if (task.IsCompleted || enabled) return task.Result;", false)]
    [Arguments("if (!(enabled || !task.IsCompleted)) return task.Result;", true)]
    [Arguments("if (!(!task.IsCompleted || enabled)) return task.Result;", true)]
    [Arguments("if (!(enabled || other.IsCompleted)) return task.Result;", false)]
    [Arguments("if (!(enabled && !task.IsCompleted)) return task.Result;", false)]
    [Arguments("if (task.IsCompleted == true) return task.Result;", false)]
    [Arguments("if (task.IsCompleted is true) return task.Result;", false)]
    [Arguments("if (task.IsFaulted) return task.Result;", false)]
    [Arguments("if (other.IsCompleted) return task.Result;", false)]
    [Arguments("if (enabled) return task.Result;", false)]
    [Arguments("if (+enabled) return task.Result;", false)]
    [Arguments("if (task.Result > 0) return 1;", false)]
    [Arguments("return task.IsCompleted ? task.Result : 0;", true)]
    [Arguments("return !task.IsCompleted ? 0 : task.Result;", true)]
    [Arguments("return task.IsCompleted ? 0 : task.Result;", false)]
    [Arguments("return !task.IsCompleted ? task.Result : 0;", false)]
    [Arguments("return task.Result > 0 ? 1 : 0;", false)]
    [Arguments("return task.IsCompleted && task.Result > 0;", true)]
    [Arguments("return !task.IsCompleted || task.Result > 0;", true)]
    [Arguments("return task.IsCompleted || task.Result > 0;", false)]
    [Arguments("return !task.IsCompleted && task.Result > 0;", false)]
    [Arguments("return task.Result > 0 && task.IsCompleted;", false)]
    [Arguments("return task.IsCompleted & (task.Result > 0);", false)]
    [Arguments("if (!task.IsCompleted) { return 0; } else { return task.Result; }", false)]
    public async Task BranchConditionMustProveCompletionAsync(string source, bool expected)
    {
        var root = SyntaxFactory.ParseCompilationUnit($"class C {{ async void M() {{ {source} }} }}");
        var blocking = FindResult(root);

        await Assert.That(CompletionGuard.IsProvablyComplete(blocking, blocking.Expression)).IsEqualTo(expected);
    }

    /// <summary>Verifies only an earlier await or an exiting incomplete-task guard establishes completion.</summary>
    /// <param name="source">A block containing statements before and after the result read.</param>
    /// <param name="expected">Whether preceding statements prove completion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{ if (!task.IsCompleted) return; Use(task.Result); }", true)]
    [Arguments("{ if (!task.IsCompleted) throw new Exception(); Use(task.Result); }", true)]
    [Arguments("{ while (true) { if (!task.IsCompleted) break; Use(task.Result); } }", true)]
    [Arguments("{ while (true) { if (!task.IsCompleted) continue; Use(task.Result); } }", true)]
    [Arguments("{ if (!task.IsCompleted) { Use(0); return; } Use(task.Result); }", true)]
    [Arguments("{ if (!task.IsCompleted) { } Use(task.Result); }", false)]
    [Arguments("{ if (!task.IsCompleted) Use(0); Use(task.Result); }", false)]
    [Arguments("{ if (!task.IsCompleted) { return; } else { Use(0); } Use(task.Result); }", false)]
    [Arguments("{ if (task.IsCompleted) return; Use(task.Result); }", false)]
    [Arguments("{ await task; Use(task.Result); }", true)]
    [Arguments("{ await task.ConfigureAwait(false); Use(task.Result); }", true)]
    [Arguments("{ await other; Use(task.Result); }", false)]
    [Arguments("{ var value = await task; Use(task.Result); }", true)]
    [Arguments("{ var value = await task.ConfigureAwait(false); Use(task.Result); }", true)]
    [Arguments("{ var value = await other; Use(task.Result); }", false)]
    [Arguments("{ int value; Use(task.Result); }", false)]
    [Arguments("{ var value = task; Use(task.Result); }", false)]
    [Arguments("{ int first = await task, second = 0; Use(task.Result); }", false)]
    [Arguments("{ Use(task.Result); await task; }", false)]
    [Arguments("{ Use(0); Use(task.Result); }", false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EarlierStatementsMustEstablishCompletionAsync(string source, bool expected) => BranchConditionMustProveCompletionAsync(source, expected);

    /// <summary>Verifies repeated field paths are stable while calls, indexing, and parenthesized targets are not recognized.</summary>
    /// <param name="target">The repeated expression that produces a task.</param>
    /// <param name="expected">Whether the expression is considered stable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("task", true)]
    [Arguments("this", true)]
    [Arguments("this.task", true)]
    [Arguments("holder.task", true)]
    [Arguments("this.holder.task", true)]
    [Arguments("Load()", false)]
    [Arguments("Load().task", false)]
    [Arguments("tasks[0]", false)]
    [Arguments("(task)", false)]
    [Arguments("holder.Task<int>", false)]
    [Arguments("holder->task", false)]
    public async Task CompletionCheckRequiresAStableTargetAsync(string target, bool expected)
    {
        var root = SyntaxFactory.ParseStatement($"if ({target}.IsCompleted) return {target}.Result;");
        var blocking = FindResult(root);

        await Assert.That(CompletionGuard.IsProvablyComplete(blocking, blocking.Expression)).IsEqualTo(expected);
    }

    /// <summary>Verifies a deferred function cannot borrow a guard from its enclosing function.</summary>
    /// <param name="body">The enclosing method body.</param>
    /// <param name="expected">Whether the function containing the wait establishes completion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (task.IsCompleted) { Func<int> read = () => task.Result; }", false)]
    [Arguments("if (task.IsCompleted) { Func<int> read = delegate { return task.Result; }; }", false)]
    [Arguments("if (task.IsCompleted) { int Read() => task.Result; }", false)]
    [Arguments("Func<int> read = () => task.IsCompleted ? task.Result : 0;", true)]
    [Arguments("int Read() { if (task.IsCompleted) return task.Result; return 0; }", true)]
    [Arguments("Use(task.Result);", false)]
    public async Task CompletionProofStopsAtTheContainingFunctionAsync(string body, bool expected)
    {
        var root = SyntaxFactory.ParseCompilationUnit($"class C {{ void M() {{ {body} }} }}");
        var blocking = FindResult(root);

        await Assert.That(CompletionGuard.IsProvablyComplete(blocking, blocking.Expression)).IsEqualTo(expected);
    }

    /// <summary>Verifies a detached expression has no enclosing completion proof.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedResultHasNoCompletionProofAsync()
    {
        var blocking = (MemberAccessExpressionSyntax)SyntaxFactory.ParseExpression("task.Result");

        await Assert.That(CompletionGuard.IsProvablyComplete(blocking, blocking.Expression)).IsFalse();
    }

    /// <summary>Records that an else clause currently prevents the analyzer from recognizing its negated completion guard.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NegatedGuardElseBranchIsCurrentlyReportedAsync() =>
        VerifyBlocking.VerifyAnalyzerAsync("""
                                          using System.Threading.Tasks;

                                          class C
                                          {
                                              int Read(Task<int> task)
                                              {
                                                  if (!task.IsCompleted)
                                                  {
                                                      return 0;
                                                  }
                                                  else
                                                  {
                                                      return {|PSH1315:task.Result|};
                                                  }
                                              }
                                          }
                                          """);

    /// <summary>Finds the single result access whose ancestors are being analyzed.</summary>
    /// <param name="root">The parsed syntax fragment.</param>
    /// <returns>The result member access.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MemberAccessExpressionSyntax FindResult(SyntaxNode root) =>
        root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Single(static member => member.Name.Identifier.ValueText == "Result");
}
