// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

    /// <summary>Verifies an async void method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidMethodReportedAsync()
        => await VerifyAsyncVoid.VerifyAnalyzerAsync(
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
    [Test]
    public async Task AsyncVoidMethodFixedToTaskAsync()
        => await VerifyAsyncVoid.VerifyCodeFixAsync(AsyncVoidMethodSource, AsyncVoidMethodFixed);

    /// <summary>Verifies an async void local function is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidLocalFunctionReportedAsync()
        => await VerifyAsyncVoid.VerifyAnalyzerAsync(
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
    [Test]
    public async Task AsyncVoidActionLambdaReportedAsync()
        => await VerifyAsyncVoid.VerifyAnalyzerAsync(
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
    [Test]
    public async Task EventHandlerMethodIsCleanAsync()
        => await VerifyAsyncVoid.VerifyAnalyzerAsync(
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
    [Test]
    public async Task EventHandlerLambdaIsCleanAsync()
        => await VerifyAsyncVoid.VerifyAnalyzerAsync(
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
    [Test]
    public async Task AsyncTaskMethodIsCleanAsync()
        => await VerifyAsyncVoid.VerifyAnalyzerAsync(
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
    [Test]
    public async Task AsyncVoidOverrideIsCleanAsync()
        => await VerifyAsyncVoid.VerifyAnalyzerAsync(
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
