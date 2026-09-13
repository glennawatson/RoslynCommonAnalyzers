// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1301AwaitSingleTaskDirectlyAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests single-task combinators and preservation of observed generic results.</summary>
public class AwaitSingleTaskDirectlyAnalyzerUnitTest
{
    /// <summary>Verifies direct awaits and blocking calls report every recognized single-task shape.</summary>
    /// <param name="statement">The marked statement under test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("await {|PSH1301:Task.WhenAll(task)|};")]
    [Arguments("await {|PSH1301:Task.WhenAll(value)|};")]
    [Arguments("await {|PSH1301:Task.WhenAll<int>(value)|};")]
    [Arguments("{|PSH1301:Task.WaitAll(task)|};")]
    [Arguments("{|PSH1301:Task.WaitAll(value)|};")]
    [Arguments("var combined = {|PSH1301:Task.WhenAll(task)|};")]
    [Arguments("_ = {|PSH1301:Task.WhenAll(task)|};")]
    [Arguments("await {|PSH1301:Task.WhenAll(task)|}.ConfigureAwait(false);")]
    [Arguments("await ({|PSH1301:Task.WhenAll(task)|});")]
    [Arguments("await {|PSH1301:System.Threading.Tasks.Task.WhenAll(task)|};")]
    public async Task SingleTaskIsReportedAsync(string statement)
    {
        var source = $$"""
            using System.Threading.Tasks;
            public class C
            {
                public async Task M(Task task, Task<int> value)
                {
                    {{statement}}
                    await Task.CompletedTask;
                }
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies returning a non-generic wrapper is safe to diagnose.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnedNonGenericTaskIsReportedAsync()
    {
        const string Source = """
            using System.Threading.Tasks;
            public class C { public Task M(Task task) => {|PSH1301:Task.WhenAll(task)|}; }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies generic results and indirect await shapes are conservatively preserved.</summary>
    /// <param name="statement">The statement that must keep its combinator.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("var result = await Task.WhenAll(task);")]
    [Arguments("_ = await Task.WhenAll(task);")]
    [Arguments("Task<int[]> combined = Task.WhenAll(task);")]
    [Arguments("_ = Task.WhenAll(task);")]
    [Arguments("await (Task.WhenAll(task));")]
    [Arguments("await Task.WhenAll(task).ConfigureAwait(false);")]
    [Arguments("System.Func<Task<int[]>> factory = () => Task.WhenAll(task);")]
    public async Task ObservedOrIndirectGenericResultIsCleanAsync(string statement)
    {
        var source = $$"""
            using System.Threading.Tasks;
            public class C
            {
                public async Task M(Task<int> task)
                {
                    {{statement}}
                    await Task.CompletedTask;
                }
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies returned generic arrays remain intact, including expression-bodied awaits.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReturnedGenericResultIsCleanAsync()
    {
        const string Source = """
            using System.Threading.Tasks;
            public class C
            {
                public Task<int[]> M(Task<int> task) => Task.WhenAll(task);
                public async Task<int[]> N(Task<int> task) => await Task.WhenAll(task);
                public async Task<int[]> P(Task<int> task) { return await Task.WhenAll(task); }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies collections, different arities, other methods, and using-static calls remain silent.</summary>
    /// <param name="statement">The near-miss statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("await Task.WhenAll();")]
    [Arguments("await Task.WhenAll(task, task);")]
    [Arguments("Task.WaitAll();")]
    [Arguments("Task.WaitAll(task, task);")]
    [Arguments("await Task.WhenAll(tasks);")]
    [Arguments("Task.WaitAll(tasks);")]
    [Arguments("await Task.WhenAll(new[] { task });")]
    [Arguments("await Task.WhenAll(values);")]
    [Arguments("await Task.WhenAll(sequence);")]
    [Arguments("await Task.WhenAll(list);")]
    [Arguments("await Task.WhenAny(task);")]
    [Arguments("Task.WaitAny(task);")]
    [Arguments("await WhenAll(task);")]
    [Arguments("WaitAll(task);")]
    [Arguments("await task;")]
    [Arguments("task.Wait();")]
    [Arguments("await Task.WhenAll(null);")]
    public async Task NonSingleTaskShapeIsCleanAsync(string statement)
    {
        var source = $$"""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using static System.Threading.Tasks.Task;
            public class C
            {
                public async Task M(Task task, Task[] tasks, Task<int>[] values,
                    IEnumerable<Task> sequence, List<Task> list)
                {
                    {{statement}}
                    await Task.CompletedTask;
                }
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies nullable annotations and unconstrained result types retain their task classification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NullableAndGenericTasksAreReportedAsync()
    {
        const string Source = """
            #nullable enable
            using System.Threading.Tasks;
            public class C
            {
                public async Task M<T>(Task? task, Task<T>? value)
                {
                    await {|PSH1301:Task.WhenAll(task!)|};
                    await {|PSH1301:Task.WhenAll(value!)|};
                }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies derived tasks and task-constrained type parameters are not classified as exact Task types.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DerivedTaskTypesAreCleanAsync()
    {
        const string Source = """
            using System;
            using System.Threading.Tasks;
            public class Derived : Task { public Derived() : base(() => { }) { } }
            public class C
            {
                public async Task M<T>(T constrained, Derived derived) where T : Task
                {
                    await Task.WhenAll(constrained);
                    await Task.WhenAll(derived);
                }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies foreign static and instance methods cannot pass the Task binding gate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForeignCombinatorsAreCleanAsync()
    {
        const string Source = """
            using System.Threading.Tasks;
            public class Other
            {
                public static Task WhenAll(Task task) => task;
                public void WaitAll(Task task) { }
            }
            public class C
            {
                public Task M(Task task)
                {
                    new Other().WaitAll(task);
                    return Other.WhenAll(task);
                }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an overload resolution failure does not produce an analyzer diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedCombinatorIsCleanAsync()
    {
        const string Source = """
            using System.Threading.Tasks;
            public class C { public void M() { Task.WaitAll({|CS1503:42|}); } }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies syntax rejection resets the out parameter, and both combinator names are recognized.</summary>
    /// <param name="expression">The invocation syntax.</param>
    /// <param name="matches">Whether the call passes the shape gate.</param>
    /// <param name="isWaitAll">Whether it is the blocking combinator.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Task.WhenAll(t)", true, false)]
    [Arguments("Task.WhenAll<int>(t)", true, false)]
    [Arguments("Task.WaitAll(t)", true, true)]
    [Arguments("Task.WhenAll()", false, false)]
    [Arguments("Task.WhenAll(a, b)", false, false)]
    [Arguments("WhenAll(t)", false, false)]
    [Arguments("Task.WhenAny(t)", false, false)]
    public async Task CombinatorShapeIsClassifiedAsync(string expression, bool matches, bool isWaitAll)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var actual = Psh1301AwaitSingleTaskDirectlyAnalyzer.IsSingleArgumentCombinatorShape(invocation, out var waitAll);
        await Assert.That(actual).IsEqualTo(matches);
        await Assert.That(waitAll).IsEqualTo(isWaitAll);
    }

    /// <summary>Verifies missing task definitions and missing generic definitions both close the applicable gate.</summary>
    /// <param name="taskDefinition">The deliberately incomplete task surface.</param>
    /// <param name="statement">The consumer statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "Other.WhenAll(null);")]
    [Arguments("namespace System.Threading.Tasks { public class Task { public static void WhenAll(object task) { } } }", "System.Threading.Tasks.Task.WhenAll(null);")]
    [Arguments("namespace System.Threading.Tasks { public class Task { public static void WhenAll(object task) { } } }", "System.Threading.Tasks.Task.WhenAll(new object());")]
    [Arguments(
        "namespace System.Threading.Tasks { public class Task { public static void WhenAll(Task task) { } } }",
        "{|PSH1301:System.Threading.Tasks.Task.WhenAll(new System.Threading.Tasks.Task())|};")]
    public async Task MissingFrameworkTaskDefinitionsAreHandledAsync(string taskDefinition, string statement)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                namespace System
                {
                    public class Object { }
                    public class ValueType { }
                    public struct Void { }
                }
                {{taskDefinition}}
                public class Other { public static void WhenAll(object task) { } }
                public class C { public void M() { {{statement}} } }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectMetadataReferences(projectId, []));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a marked-source test against the cached .NET 9 reference set.</summary>
    /// <param name="source">The consumer source and expected diagnostics.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source };
        await test.RunAsync(CancellationToken.None);
    }
}
