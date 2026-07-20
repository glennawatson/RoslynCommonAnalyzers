// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1501TwoParameterMiddlewareAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1501TwoParameterMiddlewareAnalyzer"/> (PSH1501 legacy nested-delegate middleware).</summary>
public class TwoParameterMiddlewareAnalyzerUnitTest
{
    /// <summary>Minimal middleware-pipeline surfaces under the real ASP.NET Core namespaces, so the metadata-name probes resolve.</summary>
    private const string AspNetCoreStubsSource = """
        namespace Microsoft.AspNetCore.Http
        {
            public abstract class HttpContext
            {
            }

            public delegate System.Threading.Tasks.Task RequestDelegate(HttpContext context);
        }

        namespace Microsoft.AspNetCore.Builder
        {
            using System;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;

            public interface IApplicationBuilder
            {
                IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware);
            }

            public sealed class WebApplication : IApplicationBuilder
            {
                public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => this;
            }

            public static class UseExtensions
            {
                public static IApplicationBuilder Use(this IApplicationBuilder app, Func<HttpContext, RequestDelegate, Task> middleware) => app;
            }
        }
        """;

    /// <summary>Verifies a nested async-lambda middleware is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedAsyncLambdaIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using System.Threading.Tasks;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use({|PSH1501:next => async context =>
                    {
                        await next(context);
                    }|});
            }
            """);

    /// <summary>Verifies a nested non-async lambda middleware is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedLambdaIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use({|PSH1501:next => context => next(context)|});
            }
            """);

    /// <summary>Verifies a parenthesized single-parameter outer lambda is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedSingleParameterIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use({|PSH1501:(next) => context => next(context)|});
            }
            """);

    /// <summary>Verifies a block-bodied outer lambda that returns a nested delegate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodyReturningLambdaIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use({|PSH1501:next =>
                    {
                        return context => next(context);
                    }|});
            }
            """);

    /// <summary>Verifies a nested anonymous-method inner delegate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedAnonymousMethodIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using System.Threading.Tasks;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use({|PSH1501:next => delegate(HttpContext context)
                    {
                        return next(context);
                    }|});
            }
            """);

    /// <summary>Verifies the legacy form is reported on a concrete type implementing the builder interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WebApplicationReceiverIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(WebApplication app)
                    => app.Use({|PSH1501:next => context => next(context)|});
            }
            """);

    /// <summary>Verifies the legacy form is reported through a conditional-access invocation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessReceiverIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app?.Use({|PSH1501:next => context => next(context)|});
            }
            """);

    /// <summary>Verifies a nested delegate wrapped in parentheses is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedInnerDelegateIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use({|PSH1501:next => (context => next(context))|});
            }
            """);

    /// <summary>Verifies the outer lambda is reported when the whole argument is parenthesized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedArgumentIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use(({|PSH1501:next => context => next(context)|}));
            }
            """);

    /// <summary>Verifies an unrelated single-parameter Use overload on a builder is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLegacyUseOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class MiddlewareHost : IApplicationBuilder
            {
                public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) => this;

                public void Use(Func<int, Func<int, int>> factory)
                {
                }
            }

            public class C
            {
                public void M(MiddlewareHost host)
                    => host.Use(next => x => x);
            }
            """);

    /// <summary>Verifies the modern two-parameter overload is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModernTwoParameterLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using System.Threading.Tasks;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use(async (context, next) =>
                    {
                        await next(context);
                    });
            }
            """);

    /// <summary>Verifies a middleware that returns its next delegate unchanged is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnsNextUnchangedIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use(next => next);
            }
            """);

    /// <summary>Verifies a middleware whose inner value is a method call, not a lambda, is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InnerMethodCallIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use(next => Wrap(next));

                private static RequestDelegate Wrap(RequestDelegate next) => next;
            }
            """);

    /// <summary>Verifies a block-bodied outer lambda that returns an existing delegate is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodyWithoutReturnedLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IApplicationBuilder app)
                    => app.Use(next =>
                    {
                        System.Console.WriteLine("configuring");
                        return next;
                    });
            }
            """);

    /// <summary>Verifies a same-shaped Use on a non-builder type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonBuilderUseIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using Microsoft.AspNetCore.Http;

            public class Widget : IDisposable
            {
                public void Use(Func<RequestDelegate, RequestDelegate> middleware)
                {
                }

                public void Dispose()
                {
                }
            }

            public class C
            {
                public void M(Widget widget)
                    => widget.Use(next => context => next(context));
            }
            """);

    /// <summary>Verifies a conditional-access call to a member other than Use is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessToOtherMemberIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app)
                {
                    app?.ToString();
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the ASP.NET Core builder type is not referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithoutAspNetCoreIsCleanAsync()
        => await VerifyWithoutStubsAsync(
            """
            using System;

            namespace MyApp
            {
                public delegate void Handler();

                public class Pipeline
                {
                    public void Use(Func<Handler, Handler> middleware)
                    {
                    }
                }

                public class C
                {
                    public void M(Pipeline pipeline)
                        => pipeline.Use(next => () => next());
                }
            }
            """);

    /// <summary>Runs a verification with the ASP.NET Core stub surfaces added to the compilation.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.Sources.Add(AspNetCoreStubsSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification without the ASP.NET Core stubs, to exercise the marker gate.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithoutStubsAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
