// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCapture = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2479CapturedLoopVariableAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2479 (a loop-stepped variable captured by an escaping delegate).</summary>
public class CapturedLoopVariableAnalyzerUnitTest
{
    /// <summary>Verifies a for control variable captured by a delegate added to a collection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForVariableAddedToCollectionIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task ForVariableSubscribedToEventIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task ForVariableHandedToTaskRunIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task WhileSteppedLocalIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task DoSteppedLocalIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task ForVariableYieldedIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task AnonymousMethodCapturingForVariableIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task ForVariableAssignedToArrayElementIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task LocalFunctionCapturingForVariableIsReportedAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task ForEachIterationVariableIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task ImmediatelyInvokedLambdaIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task NonVaryingCapturedLocalIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task PerIterationCopyIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task CapturedParameterIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task LambdaInvokedInPlaceIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task ReturnedDelegateIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task LocalFunctionCalledInPlaceIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task DelegateOutsideLoopIsCleanAsync()
        => await VerifyCleanAsync(
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
        var test = new VerifyCapture.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyReportAsync(source);
}
