// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2707FireAndForgetHttpContextAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2707 (a fire-and-forget Task.Run in a controller captures the request HttpContext).</summary>
public class Sst2707FireAndForgetHttpContextAnalyzerUnitTest
{
    /// <summary>The inline stub of the ASP.NET Core request/controller surface the rule gates on.</summary>
    private const string AspNetCoreStub =
        """

        namespace Microsoft.AspNetCore.Http
        {
            public abstract class HttpContext
            {
                public abstract object User { get; }
            }
        }

        namespace Microsoft.AspNetCore.Mvc
        {
            public abstract class ControllerBase
            {
                public Microsoft.AspNetCore.Http.HttpContext HttpContext { get; } = null!;
            }
        }
        """;

    /// <summary>Verifies a discarded <c>Task.Run</c> whose lambda captures <c>HttpContext</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FireAndForgetCapturingHttpContextIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public void Index()
                {
                    {|SST2707:Task.Run(() => { var user = HttpContext.User; })|};
                }
            }
            """);

    /// <summary>Verifies the anonymous-method delegate form is also reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousMethodCapturingHttpContextIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public void Index()
                {
                    {|SST2707:Task.Run(delegate { var user = HttpContext.User; })|};
                }
            }
            """);

    /// <summary>Verifies a <c>_ = Task.Run(...)</c> discard assignment capturing <c>HttpContext</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardAssignmentCapturingHttpContextIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public void Index()
                {
                    _ = {|SST2707:Task.Run(() => HttpContext)|};
                }
            }
            """);

    /// <summary>Verifies an awaited <c>Task.Run</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedTaskRunIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public async Task IndexAsync()
                {
                    await Task.Run(() => { var user = HttpContext.User; });
                }
            }
            """);

    /// <summary>Verifies a returned <c>Task.Run</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnedTaskRunIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public Task IndexAsync()
                {
                    return Task.Run(() => { var user = HttpContext.User; });
                }
            }
            """);

    /// <summary>Verifies a <c>Task.Run</c> assigned to a real target (not a discard) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignedTaskRunIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                private Task _pending = Task.CompletedTask;

                public void Index()
                {
                    _pending = Task.Run(() => { var user = HttpContext.User; });
                }
            }
            """);

    /// <summary>Verifies a discarded <c>Task.Run</c> that does not touch <c>HttpContext</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskRunWithoutHttpContextIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public void Index()
                {
                    Task.Run(() => Log());
                }

                private static void Log()
                {
                }
            }
            """);

    /// <summary>Verifies the same capture outside a controller (no <c>ControllerBase</c> ancestor) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FireAndForgetOutsideControllerIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;

            public class Worker
            {
                public void Run(HttpContext context)
                {
                    Task.Run(() => { var user = context.User; });
                }
            }
            """);

    /// <summary>Verifies a discarded call to a same-named non-Task helper is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Exercises the simple-lambda and zero-argument shapes and the non-<c>Task.Run</c> binding rejection.</remarks>
    [Test]
    public async Task CustomRunHelperIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public void Schedule()
                {
                    Run(item => item.ToString());
                    Run();
                }

                private static void Run(System.Func<object, string> selector)
                {
                }

                private static void Run()
                {
                }
            }
            """);

    /// <summary>Verifies a discarded <c>Task.Run</c> passed a method group (not an inline delegate) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupTaskRunIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public void Schedule()
                {
                    Task.Run(Work);
                }

                private static void Work()
                {
                }
            }
            """);

    /// <summary>Verifies a discarded invocation whose callee is not a simple name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvokedExpressionWithoutSimpleNameIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class HomeController : ControllerBase
            {
                public void Invoke()
                {
                    Resolve()();
                }

                private static System.Action Resolve() => () => { };
            }
            """);

    /// <summary>Verifies the rule stays silent when the ASP.NET Core surface is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithoutAspNetCoreReferenceIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode =
                """
                using System.Threading.Tasks;

                public class Worker
                {
                    public void Index()
                    {
                        Task.Run(() => System.Console.WriteLine("work"));
                    }
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core stub appended.</summary>
    /// <param name="source">The source with optional diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + AspNetCoreStub,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
