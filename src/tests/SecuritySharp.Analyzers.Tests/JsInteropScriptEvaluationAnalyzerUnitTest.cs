// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeInterop = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1702JsInteropScriptEvaluationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1702 (a JavaScript interop call targeting a script-evaluation primitive).</summary>
public class JsInteropScriptEvaluationAnalyzerUnitTest
{
    /// <summary>Inline stubs of the JS interop surface the rule gates on.</summary>
    private const string InteropStub =
        """

        namespace Microsoft.JSInterop
        {
            public interface IJSRuntime
            {
                System.Threading.Tasks.ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args);
            }

            public interface IJSObjectReference : System.IAsyncDisposable
            {
                System.Threading.Tasks.ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args);
            }

            public static class JSRuntimeExtensions
            {
                public static System.Threading.Tasks.ValueTask InvokeVoidAsync(this IJSRuntime jsRuntime, string identifier, params object[] args)
                    => default;
            }
        }
        """;

    /// <summary>An interop stub with <c>IJSRuntime</c> present but no <c>IJSObjectReference</c>.</summary>
    private const string RuntimeOnlyStub =
        """

        namespace Microsoft.JSInterop
        {
            public interface IJSRuntime
            {
                System.Threading.Tasks.ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args);
            }

            public static class JSRuntimeExtensions
            {
                public static System.Threading.Tasks.ValueTask InvokeVoidAsync(this IJSRuntime jsRuntime, string identifier, params object[] args)
                    => default;
            }
        }
        """;

    /// <summary>Verifies <c>InvokeVoidAsync("eval", ...)</c> on <c>IJSRuntime</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvokeVoidAsyncEvalReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js, string code) => js.InvokeVoidAsync({|SES1702:"eval"|}, code);
            }
            """);

    /// <summary>Verifies the generic <c>InvokeAsync</c> naming the <c>Function</c> constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvokeAsyncFunctionReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask<string> M(IJSRuntime js, object[] args) => js.InvokeAsync<string>({|SES1702:"Function"|}, args);
            }
            """);

    /// <summary>Verifies <c>document.write</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentWriteReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js, string html) => js.InvokeVoidAsync({|SES1702:"document.write"|}, html);
            }
            """);

    /// <summary>Verifies <c>document.writeln</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentWritelnReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js, string html) => js.InvokeVoidAsync({|SES1702:"document.writeln"|}, html);
            }
            """);

    /// <summary>Verifies an eval call through an <c>IJSObjectReference</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectReferenceEvalReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask<string> M(IJSObjectReference js, object[] args) => js.InvokeAsync<string>({|SES1702:"eval"|}, args);
            }
            """);

    /// <summary>Verifies a call on a concrete <c>IJSRuntime</c> implementation is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteRuntimeImplementationReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask<string> M(MyRuntime js, object[] args) => js.InvokeAsync<string>({|SES1702:"eval"|}, args);
            }

            public sealed class MyRuntime : IJSRuntime
            {
                public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args) => default;
            }
            """);

    /// <summary>Verifies a constant-field identifier of <c>eval</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstFieldIdentifierReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                private const string Fn = "eval";

                public ValueTask M(IJSRuntime js, string code) => js.InvokeVoidAsync({|SES1702:Fn|}, code);
            }
            """);

    /// <summary>Verifies <c>setTimeout</c> with a string body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetTimeoutStringBodyReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js, string body) => js.InvokeVoidAsync({|SES1702:"setTimeout"|}, body, 100);
            }
            """);

    /// <summary>Verifies <c>setInterval</c> with a string-literal body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetIntervalStringLiteralBodyReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js) => js.InvokeVoidAsync({|SES1702:"setInterval"|}, "doWork()", 1000);
            }
            """);

    /// <summary>Verifies <c>setTimeout</c> passed a non-string handler reference is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetTimeoutFunctionReferenceIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js, object handler) => js.InvokeVoidAsync("setTimeout", handler, 100);
            }
            """);

    /// <summary>Verifies <c>setTimeout</c> with no following argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetTimeoutWithoutBodyIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js) => js.InvokeVoidAsync("setTimeout");
            }
            """);

    /// <summary>Verifies <c>setTimeout</c> passed a typeless <c>null</c> body is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetTimeoutNullBodyIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js) => js.InvokeVoidAsync("setTimeout", null, 100);
            }
            """);

    /// <summary>Verifies an ordinary named function identifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BenignIdentifierIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js, string data) => js.InvokeVoidAsync("app.render", data);
            }
            """);

    /// <summary>Verifies a non-constant identifier is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantIdentifierIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.JSInterop;

            public class C
            {
                public ValueTask M(IJSRuntime js, string name, string data) => js.InvokeVoidAsync(name, data);
            }
            """);

    /// <summary>Verifies a call with no arguments at all is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoArgumentsIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.JSInterop;

            public class C
            {
                public void M(Widget widget) => widget.InvokeVoidAsync();
            }

            public sealed class Widget
            {
                public void InvokeVoidAsync() { }
            }
            """);

    /// <summary>Verifies a same-named invoke on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedInvokeOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.JSInterop;

            public class C
            {
                public void M(Widget widget, string code) => widget.InvokeVoidAsync("eval", code);
            }

            public sealed class Widget
            {
                public void InvokeVoidAsync(string identifier, string arg) { }
            }
            """);

    /// <summary>Verifies the rule fires when only <c>IJSRuntime</c> (not <c>IJSObjectReference</c>) is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeOnlyStubReportedAsync()
    {
        var test = new AnalyzeInterop.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Threading.Tasks;
                       using Microsoft.JSInterop;

                       public class C
                       {
                           public ValueTask M(IJSRuntime js, string code) => js.InvokeVoidAsync({|SES1702:"eval"|}, code);
                       }
                       """ + RuntimeOnlyStub,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a same-named unrelated call is not reported when <c>IJSObjectReference</c> is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeOnlyUnrelatedTypeIsCleanAsync()
    {
        var test = new AnalyzeInterop.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       public class C
                       {
                           public void M(Widget widget, string code) => widget.InvokeVoidAsync("eval", code);
                       }

                       public sealed class Widget
                       {
                           public void InvokeVoidAsync(string identifier, string arg) { }
                       }
                       """ + RuntimeOnlyStub,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent when the JS interop surface is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenRuntimeUnavailableAsync()
    {
        var test = new AnalyzeInterop.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       namespace NotBlazor
                       {
                           public interface IJSRuntime
                           {
                               void InvokeVoidAsync(string identifier, string arg);
                           }

                           public class C
                           {
                               public void M(IJSRuntime js, string code) => js.InvokeVoidAsync("eval", code);
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline JS interop stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeInterop.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + InteropStub,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
