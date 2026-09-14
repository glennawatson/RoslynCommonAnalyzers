// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using RoslynCommon.Analyzers.Tests;

using VerifyCapture = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2479CapturedLoopVariableAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2479 (a loop-stepped variable captured by an escaping delegate).</summary>
public class CapturedLoopVariableAnalyzerUnitTest
{
    /// <summary>Checks prefix, assignment, and by-reference mutations make escaping captures unsafe.</summary>
    /// <param name="mutation">The statement changing the captured local.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("++i;")]
    [Arguments("--i;")]
    [Arguments("i--;")]
    [Arguments("i = i + 1;")]
    [Arguments("Step(ref i);")]
    [Arguments("Reset(out i);")]
    public Task LoopMutationFormsAreReportedAsync(string mutation) =>
        VerifyReportAsync($$"""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(List<Action> actions)
                {
                    int i = 0;
                    while (i < 3)
                    {
                        actions.Add({|SST2479:() => Use(i)|});
                        {{mutation}}
                    }
                }
                static void Step(ref int value) { value++; }
                static void Reset(out int value) { value = 3; }
                static void Use(int value) { }
            }
            """);

    /// <summary>Checks mutation nested inside a for incrementor still advances the captured local.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedIncrementorMutationIsReportedAsync() =>
        VerifyReportAsync("""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(List<Action> actions)
                {
                    int i = 0;
                    for (; i < 3; Step(++i))
                    {
                        actions.Add({|SST2479:delegate { Use(i); }|});
                    }
                }
                static void Step(int value) { }
                static void Use(int value) { }
            }
            """);

    /// <summary>Checks qualified deferred runners and cast delegates retain capture diagnostics.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeferredRunnerAndCastShapesAreReportedAsync() =>
        VerifyReportAsync("""
            using System;
            class C
            {
                void M()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        System.Threading.Tasks.Task.Factory.StartNew({|SST2479:() => Use(i)|});
                        System.Threading.ThreadPool.QueueUserWorkItem({|SST2479:_ => Use(i)|});
                        Add((Action)({|SST2479:() => Use(i)|}));
                    }
                }
                static void Add(Action action) { }
                static void Use(int value) { }
            }
            """);

    /// <summary>Checks each recognized collection-store spelling treats a capturing argument as escaping.</summary>
    /// <param name="name">The store method name.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("AddRange")]
    [Arguments("AddFirst")]
    [Arguments("AddLast")]
    [Arguments("Insert")]
    [Arguments("Enqueue")]
    [Arguments("Push")]
    [Arguments("TryAdd")]
    public Task CollectionStoreNamesReportEscapingCapturesAsync(string name) =>
        VerifyReportAsync($$"""
            using System;
            class C
            {
                void M()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        {{name}}({|SST2479:() => GC.KeepAlive(i)|});
                    }
                }
                void {{name}}(Action value) { }
            }
            """);

    /// <summary>Checks calls with similar names, nested delegate writes, and ordinary storage are ignored.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonEscapingAndNestedMutationsAreIgnoredAsync() =>
        VerifyCleanAsync("""
            using System;
            using System.Collections.Generic;
            class C
            {
                void M(List<Action> actions)
                {
                    int value = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        Action local = () => value++;
                        void Nested() { value++; }
                        actions.Add(() => Use(value));
                        actions.Add(() => { int inner = 1; Use(inner); });
                        actions.Add(Ordinary);
                        actions.Add(null);
                        this.Value = i;
                        _ = -value;
                        _ = value!;
                        GetRunner().Run(() => Use(i));
                        GetRunner().StartNew(() => Use(i));
                        GetRunner().QueueUserWorkItem(() => Use(i));
                        local = () => Use(i);
                    }
                }
                static C GetRunner() => new C();
                int Value { get; set; }
                void Run(Action action) { }
                void StartNew(Action action) { }
                void QueueUserWorkItem(Action action) { }
                static void Ordinary() { }
                static void Use(int value) { }
            }
            """);

    /// <summary>Checks similar method spellings are not classified as collection stores.</summary>
    /// <param name="name">The non-store method name.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Set")]
    [Arguments("Adds")]
    [Arguments("Powh")]
    [Arguments("TryGet")]
    [Arguments("Inside")]
    [Arguments("AddMore")]
    [Arguments("Dequeue")]
    [Arguments("Enqueux")]
    [Arguments("AddLasx")]
    [Arguments("AddOther")]
    [Arguments("AddRanks")]
    [Arguments("AddFirsx")]
    public Task SimilarStoreNamesAreIgnoredAsync(string name) =>
        VerifyCleanAsync($$"""
            class C
            {
                void M()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        {{name}}(() => System.GC.KeepAlive(i));
                    }
                }
                void {{name}}(System.Action value) { }
            }
            """);

    /// <summary>Checks an incomplete yield node from a syntax producer is ignored without an expression.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task YieldWithoutExpressionIsIgnoredAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C { System.Collections.Generic.IEnumerable<int> M() { yield return 1; } }");
        var statement = root.DescendantNodes().OfType<YieldStatementSyntax>().Single();
        var tree = CSharpSyntaxTree.Create(root.ReplaceNode(statement, statement.WithExpression(null)));
        var compilation = CSharpCompilation.Create("IncompleteYield", [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Sst2479CapturedLoopVariableAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Checks locals declared inside while and do bodies have separate storage for each iteration.</summary>
    /// <param name="loop">The loop containing a per-iteration local capture.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("while (true) { int copy = 1; actions.Add(() => System.GC.KeepAlive(copy)); break; }")]
    [Arguments("do { int copy = 1; actions.Add(() => System.GC.KeepAlive(copy)); } while (false);")]
    public Task WhileAndDoIterationLocalsAreIgnoredAsync(string loop) =>
        VerifyCleanAsync($$"""class C { void M(System.Collections.Generic.List<System.Action> actions) { {{loop}} } }""");

    /// <summary>Checks top-level captures terminate ancestor searches safely when no member encloses them.</summary>
    /// <returns>The verification task.</returns>
    [Test]
    public async Task TopLevelStableCapturesAreIgnoredAsync()
    {
        var test = new VerifyCapture.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                using System;
                using System.Collections.Generic;
                var actions = new List<Action>();
                int value = 1;
                actions.Add(() => GC.KeepAlive(value));
                for (int i = 0; i < 3; i++)
                {
                    actions.Add(() => GC.KeepAlive(value));
                }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectCompilationOptions(
            projectId,
            solution.GetProject(projectId)!.CompilationOptions!.WithOutputKind(Microsoft.CodeAnalysis.OutputKind.ConsoleApplication)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a for control variable captured by a delegate added to a collection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForVariableAddedToCollectionIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers)
                {
                    for (int i = 0; i < 3; i++)
                        handlers.Add({|SST2479:() => Use(i)|});
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a for control variable captured by an event handler is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForVariableSubscribedToEventIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System.ComponentModel;

            public class C
            {
                public void M(INotifyPropertyChanged source)
                {
                    for (int i = 0; i < 3; i++)
                        source.PropertyChanged += {|SST2479:(s, e) => Use(i)|};
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a for control variable captured by a deferred runner is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForVariableHandedToTaskRunIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class C
            {
                public void M(List<Task> tasks)
                {
                    for (int i = 0; i < 3; i++)
                        tasks.Add(Task.Run({|SST2479:() => Use(i)|}));
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a local stepped by a while body and captured by an escaping delegate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WhileSteppedLocalIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers)
                {
                    int i = 0;
                    while (i < 3)
                    {
                        handlers.Add({|SST2479:() => Use(i)|});
                        i++;
                    }
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a local stepped by a do body and captured by an escaping delegate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DoSteppedLocalIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers)
                {
                    int i = 0;
                    do
                    {
                        handlers.Add({|SST2479:() => Use(i)|});
                        i++;
                    }
                    while (i < 3);
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a for control variable captured by a yielded delegate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForVariableYieldedIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public IEnumerable<Action> M()
                {
                    for (int i = 0; i < 3; i++)
                        yield return {|SST2479:() => Use(i)|};
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies an anonymous method capturing the for control variable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AnonymousMethodCapturingForVariableIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers)
                {
                    for (int i = 0; i < 3; i++)
                        handlers.Add({|SST2479:delegate { Use(i); }|});
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a delegate assigned to an array element and capturing the for variable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForVariableAssignedToArrayElementIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System;

            public class C
            {
                public void M(Action[] slots)
                {
                    for (int i = 0; i < slots.Length; i++)
                        slots[i] = {|SST2479:() => Use(i)|};
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a local function capturing the for variable and stored in a collection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionCapturingForVariableIsReportedAsync() =>
        VerifyReportAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        void Handler() => Use(i);
                        handlers.Add({|SST2479:Handler|});
                    }
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a foreach iteration variable captured by an escaping delegate is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForEachIterationVariableIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers, IEnumerable<int> items)
                {
                    for (int i = 0; i < 3; i++)
                        foreach (var item in items)
                            handlers.Add(() => Use(item));
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies an immediately invoked lambda capturing the for variable is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImmediatelyInvokedLambdaIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public void M()
                {
                    for (int i = 0; i < 3; i++)
                        ((Action)(() => Use(i)))();
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a captured local that never changes across the loop is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonVaryingCapturedLocalIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers)
                {
                    int x = 42;
                    for (int i = 0; i < 3; i++)
                        handlers.Add(() => Use(x));
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a per-iteration copy captured instead of the for variable is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PerIterationCopyIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<Action> handlers)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        int copy = i;
                        handlers.Add(() => Use(copy));
                    }
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a captured parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CapturedParameterIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(int p, List<Action> handlers)
                {
                    for (int i = 0; i < 3; i++)
                        handlers.Add(() => Use(p));
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a lambda invoked in place inside the loop is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LambdaInvokedInPlaceIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(List<int> items)
                {
                    for (int i = 0; i < 3; i++)
                        items.ForEach(x => Use(i + x));
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a delegate returned from inside the loop is never reported, because return ends the loop.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReturnedDelegateIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public Action M()
                {
                    for (int i = 0; i < 3; i++)
                        return () => Use(i);
                    return null;
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a local function called in place inside the loop is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionCalledInPlaceIsCleanAsync() =>
        VerifyCleanAsync(
            """
            public class C
            {
                public void M()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        void Handler() => Use(i);
                        Handler();
                    }
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Verifies a delegate outside any loop is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelegateOutsideLoopIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(int value, List<Action> handlers)
                {
                    handlers.Add(() => Use(value));
                }

                private void Use(int value) { }
            }
            """);

    /// <summary>Runs a report verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new VerifyCapture.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyCleanAsync(string source) => VerifyReportAsync(source);
}
