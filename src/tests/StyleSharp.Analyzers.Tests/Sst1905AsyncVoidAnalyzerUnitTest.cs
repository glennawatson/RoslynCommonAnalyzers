// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VerifyAsyncVoid = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1905AsyncVoidAnalyzer,
    StyleSharp.Analyzers.Sst1905AsyncVoidCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1905 (async void that is not a genuine event handler).</summary>
public class Sst1905AsyncVoidAnalyzerUnitTest
{
    /// <summary>An async void method to be fixed.</summary>
    private const string AsyncVoidMethodSource = """
        using System.Threading.Tasks;

        public class C
        {
            public {|SST1905:async|} void M()
            {
                await Task.Yield();
            }
        }
        """;

    /// <summary>The async void method after the fix.</summary>
    private const string AsyncVoidMethodFixed = """
        using System.Threading.Tasks;

        public class C
        {
            public async Task M()
            {
                await Task.Yield();
            }
        }
        """;

    /// <summary>Checks a missing EventArgs metadata type cannot grant the event-handler exemption.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingEventArgsDoesNotExemptAsyncVoidAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { async void M(object sender, MissingArgs args) {} }");
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> { ["SST1905"] = ReportDiagnostic.Warn });
        var compilation = CSharpCompilation.Create("MissingEventArgs", [tree], options: options);
        var diagnostics = await compilation.WithAnalyzers([new Sst1905AsyncVoidAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["SST1905"]);
    }

    /// <summary>Checks inherited signatures, event shapes, and each anonymous-function form.</summary>
    /// <param name="source">The source containing the async member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("interface I { void M(); } class C : I { public async void M() { await Task.Yield(); } }")]
    [Arguments("interface I { void M(); } class C : I { async void I.M() { await Task.Yield(); } }")]
    [Arguments("interface I { void M(int x); } class C : I { public void M(int x) {} public {|SST1905:async|} void M() { await Task.Yield(); } }")]
    [Arguments("interface I { int M { get; } } class C : I { int I.M => 0; public {|SST1905:async|} void M() { await Task.Yield(); } }")]
    [Arguments("interface I { void Other(); } class C : I { public void Other() {} public {|SST1905:async|} void M() { await Task.Yield(); } }")]
    [Arguments("class C { public {|SST1905:async|} void M(string sender, EventArgs args) { await Task.Yield(); } }")]
    [Arguments("class C { public {|SST1905:async|} void M(object sender, string args) { await Task.Yield(); } }")]
    [Arguments("class Args : EventArgs {} class C { public async void M(object sender, Args args) { await Task.Yield(); } }")]
    [Arguments("class C { async void A(object sender, EventArgs args) { await Task.Yield(); } async void B(object sender, EventArgs args) { await Task.Yield(); } }")]
    [Arguments("class C { void M() { async void Handler(object sender, EventArgs args) { await Task.Yield(); } Handler(null, EventArgs.Empty); } }")]
    [Arguments("class C { void M() { void Local() {} Local(); } }")]
    [Arguments("class C { void M() { async Task Local() { await Task.Yield(); } _ = Local(); } }")]
    [Arguments("class C { Action<int> M() => {|SST1905:async|} x => { await Task.Yield(); }; }")]
    [Arguments("class C { Action M() => {|SST1905:async|} delegate { await Task.Yield(); }; }")]
    [Arguments("class C { Func<Task> M() => async () => { await Task.Yield(); }; }")]
    [Arguments("class C { Action M() => () => {}; }")]
    public Task AsyncVoidExemptionsDependOnSignatureAsync(string source) =>
        VerifyAsyncVoid.VerifyAnalyzerAsync($"using System; using System.Threading.Tasks; {source}");

    /// <summary>Verifies an async void method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncVoidMethodReportedAsync() =>
        VerifyAsyncVoid.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public {|SST1905:async|} void M()
                {
                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies an async void method is rewritten to return Task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncVoidMethodFixedToTaskAsync() =>
        VerifyAsyncVoid.VerifyCodeFixAsync(AsyncVoidMethodSource, AsyncVoidMethodFixed);

    /// <summary>Verifies an async void local function is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncVoidLocalFunctionReportedAsync() =>
        VerifyAsyncVoid.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public void M()
                {
                    {|SST1905:async|} void Local()
                    {
                        await Task.Yield();
                    }

                    Local();
                }
            }
            """);

    /// <summary>Verifies an async void Action lambda — the fire-and-forget shape — is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncVoidActionLambdaReportedAsync() =>
        VerifyAsyncVoid.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public void M()
                {
                    Action a = {|SST1905:async|} () => await Task.Yield();
                    a();
                }
            }
            """);

    /// <summary>Verifies a genuine event-handler method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EventHandlerMethodIsCleanAsync() =>
        VerifyAsyncVoid.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public async void OnClick(object sender, EventArgs e)
                {
                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies an EventHandler-typed lambda is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EventHandlerLambdaIsCleanAsync() =>
        VerifyAsyncVoid.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public void M()
                {
                    EventHandler h = async (s, e) => await Task.Yield();
                    h(this, EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies an async Task method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncTaskMethodIsCleanAsync() =>
        VerifyAsyncVoid.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies an async void override of an inherited void member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncVoidOverrideIsCleanAsync() =>
        VerifyAsyncVoid.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public abstract class Base
            {
                protected abstract void OnTick();
            }

            public class C : Base
            {
                protected override async void OnTick()
                {
                    await Task.Yield();
                }
            }
            """);
}
