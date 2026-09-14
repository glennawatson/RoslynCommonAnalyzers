// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using RoslynCommon.Analyzers.Tests;
using VerifyConsumeOnce = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1316ConsumeValueTaskOnceAnalyzer,
    PerformanceSharp.Analyzers.Psh1316ConsumeValueTaskOnceCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1316 (a ValueTask consumed more than once, through a loop or a copy).</summary>
public class Psh1316ConsumeValueTaskOnceAnalyzerUnitTest
{
    /// <summary>A ValueTask awaited in a loop to be fixed.</summary>
    private const string AwaitInLoopSource = """
        using System.Threading.Tasks;

        public class C
        {
            private static ValueTask P() => default;

            public async Task M()
            {
                ValueTask vt = P();
                for (int i = 0; i < 3; i++)
                {
                    await {|PSH1316:vt|};
                }
            }
        }
        """;

    /// <summary>The method after the fix.</summary>
    private const string AwaitInLoopFixed = """
        using System.Threading.Tasks;

        public class C
        {
            private static ValueTask P() => default;

            public async Task M()
            {
                for (int i = 0; i < 3; i++)
                {
                    ValueTask vt = P();
                    await vt;
                }
            }
        }
        """;

    /// <summary>Verifies top-level locals are analyzed even without an enclosing member body.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TopLevelLoopConsumeIsReportedAsync()
    {
        var test = new VerifyConsumeOnce.Test
        {
            TestCode = """
                using System.Threading.Tasks;
                ValueTask vt = default;
                var copy = vt;
                while (true) { await {|PSH1316:vt|}; }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectCompilationOptions(projectId, solution.GetProject(projectId)!.CompilationOptions!.WithOutputKind(OutputKind.ConsoleApplication)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies task-shaped loops and copies are ignored when ValueTask metadata is absent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FrameworkWithoutValueTaskIsCleanAsync() =>
        new VerifyConsumeOnce.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = """
                using System.Threading.Tasks;
                class C
                {
                    async Task M(bool flag)
                    {
                        Task task = Task.CompletedTask;
                        var copy = task;
                        while (flag) { await task; }
                        await task; await copy;
                    }
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a framework exposing only one ValueTask form still identifies that form.</summary>
    /// <param name="generic">Whether the available ValueTask type is generic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SingleValueTaskFormIsRecognizedAsync(bool generic)
    {
        var declaration = generic ? "ValueTask<T>" : "ValueTask";
        var type = generic ? "ValueTask<int>" : "ValueTask";
        var test = new VerifyConsumeOnce.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = $$"""
                using System.Threading.Tasks;
                namespace System.Threading.Tasks
                {
                    public struct {{declaration}} { public int Result => 0; }
                }
                class C
                {
                    void M(bool flag)
                    {
                        {{type}} vt = default;
                        Task<int> task = Task.FromResult(1);
                        while (flag)
                        {
                            _ = task.Result;
                            _ = {|PSH1316:vt|}.Result;
                        }
                    }
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies each loop kind recognizes every token-consuming member.</summary>
    /// <param name="loop">The loop prefix.</param>
    /// <param name="ending">The suffix required by a do loop.</param>
    /// <param name="consume">The consuming expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("while (flag)", "", "_ = {|PSH1316:vt|}.Result;")]
    [Arguments("do", "while (flag);", "{|PSH1316:vt|}.GetAwaiter();")]
    [Arguments("foreach (var item in new[] { 1 })", "", "{|PSH1316:vt|}.AsTask();")]
    [Arguments("for (; flag;)", "", "await {|PSH1316:vt|}.ConfigureAwait(false);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GenericValueTaskLoopConsumesAreReportedAsync(string loop, string ending, string consume) =>
        VerifyConsumeOnce.VerifyAnalyzerAsync($$"""
            using System.Threading.Tasks;
            class C
            {
                async Task M(bool flag)
                {
                    ValueTask<int> vt = default;
                    {{loop}} { {{consume}} } {{ending}}
                }
            }
            """);

    /// <summary>Verifies reassignment, loop declarations, and nested function boundaries prevent false reports.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FreshAndDeferredConsumesAreCleanAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync("""
            using System.Threading.Tasks;
            class C
            {
                async Task M(ValueTask parameter, bool flag)
                {
                    ValueTask vt = default;
                    while (flag) { vt = default; await vt; await parameter; }
                    for (ValueTask local = default; flag;) { await local; }
                    while (flag)
                    {
                        System.Func<Task> later = async () => { await vt; };
                        async Task Local() { await vt; }
                    }
                }
            }
            """);

    /// <summary>Verifies nested loops diagnose a consume once at its nearest loop.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NestedLoopReportsOnlyOnceAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync("""
            using System.Threading.Tasks;
            class C
            {
                async Task M(bool flag)
                {
                    ValueTask vt = default;
                    while (flag) { while (flag) { await {|PSH1316:vt|}; } }
                }
            }
            """);

    /// <summary>Verifies copy scans stay within lambda, local-function, and accessor bodies.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CopiesInDifferentBodyKindsAreReportedAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync("""
            using System.Threading.Tasks;
            class C
            {
                Task M()
                {
                    System.Func<Task> action = async () =>
                    {
                        ValueTask<int> vt = default;
                        var {|PSH1316:copy|} = vt;
                        await vt; await copy;
                    };
                    async Task Local()
                    {
                        ValueTask vt = default;
                        var {|PSH1316:copy|} = vt;
                        await vt; await copy;
                    }
                    return action();
                }
                int Property
                {
                    get
                    {
                        ValueTask<int> vt = default;
                        var {|PSH1316:copy|} = vt;
                        return vt.Result + copy.Result;
                    }
                }
            }
            """);

    /// <summary>Verifies preserved copies, single-sided consumes, tasks, and using declarations are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SafeCopiesAreCleanAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync("""
            using System.Threading.Tasks;
            class C
            {
                async Task M(ValueTask parameter)
                {
                    using var resource = new System.IO.MemoryStream();
                    ValueTask original = default;
                    original = original.Preserve();
                    var preservedCopy = original;
                    await original; await preservedCopy;
                    ValueTask first = default;
                    var unused = first;
                    await first;
                    ValueTask second = default;
                    var consumed = second;
                    await consumed;
                    var parameterCopy = parameter;
                    await parameter; await parameterCopy;
                    Task task = Task.CompletedTask;
                    var taskCopy = task;
                    await task; await taskCopy;
                }
            }
            """);

    /// <summary>Verifies a declaration separated from its loop by a region is reported but not moved.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The declaration sinks into the loop body and its own statement is deleted, so the directive standing
    /// between the two ends up marking a position the declaration has left.
    /// </remarks>
    [Test]
    public async Task DeclarationAcrossADirectiveIsNotMovedAsync()
    {
        const string Source = """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
            #region Consume
                    for (int i = 0; i < 3; i++)
                    {
                        await {|PSH1316:vt|};
                    }
            #endregion
                }
            }
            """;
        await VerifyConsumeOnce.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a ValueTask declared outside a loop and awaited inside it is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AwaitInLoopReportedAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    for (int i = 0; i < 3; i++)
                    {
                        await {|PSH1316:vt|};
                    }
                }
            }
            """);

    /// <summary>Verifies a ValueTask copied into a second local, where both are consumed, is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CopyThenAwaitReportedAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    ValueTask {|PSH1316:copy|} = vt;
                    await vt;
                    await copy;
                }
            }
            """);

    /// <summary>Verifies a ValueTask created fresh inside the loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FreshInLoopIsCleanAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        ValueTask vt = P();
                        await vt;
                    }
                }
            }
            """);

    /// <summary>Verifies a preserved ValueTask awaited in a loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PreservedInLoopIsCleanAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    vt = vt.Preserve();
                    for (int i = 0; i < 3; i++)
                    {
                        await vt;
                    }
                }
            }
            """);

    /// <summary>Verifies a single consume is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleConsumeIsCleanAsync() =>
        VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    await vt;
                }
            }
            """);

    /// <summary>Verifies the fix moves the producing call into the loop.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AwaitInLoopFixedByMovingProducerAsync() =>
        VerifyConsumeOnce.VerifyCodeFixAsync(AwaitInLoopSource, AwaitInLoopFixed);
}
