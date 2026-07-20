// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeDeveloperPage = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1506UnguardedDeveloperExceptionPageAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1506 (the developer exception page must be enabled only under a development-environment guard).</summary>
public class UnguardedDeveloperExceptionPageAnalyzerUnitTest
{
    /// <summary>Inline stubs of the ASP.NET Core application-builder surface the rule gates on.</summary>
    private const string AspNetStubs = """

                                       namespace Microsoft.AspNetCore.Builder
                                       {
                                           public interface IHostEnvironment
                                           {
                                               bool IsDevelopment();
                                           }

                                           public interface IApplicationBuilder
                                           {
                                               IHostEnvironment Environment { get; }
                                           }

                                           public sealed class DeveloperExceptionPageOptions
                                           {
                                           }

                                           public static class DeveloperExceptionPageExtensions
                                           {
                                               public static IApplicationBuilder UseDeveloperExceptionPage(this IApplicationBuilder app) => app;

                                               public static IApplicationBuilder UseDeveloperExceptionPage(this IApplicationBuilder app, DeveloperExceptionPageOptions options) => app;
                                           }
                                       }
                                       """;

    /// <summary>Verifies an unguarded <c>app.UseDeveloperExceptionPage()</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnguardedCallReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app)
                {
                    {|SES1506:app.UseDeveloperExceptionPage()|};
                }
            }
            """);

    /// <summary>Verifies the static-invocation form on the extensions type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticInvocationFormReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app)
                {
                    {|SES1506:DeveloperExceptionPageExtensions.UseDeveloperExceptionPage(app)|};
                }
            }
            """);

    /// <summary>Verifies the options overload is reported when unguarded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionsOverloadReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app)
                {
                    {|SES1506:app.UseDeveloperExceptionPage(new DeveloperExceptionPageOptions())|};
                }
            }
            """);

    /// <summary>Verifies a call inside a non-development <c>if</c> is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnguardedInsideUnrelatedIfReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app, bool flag)
                {
                    if (flag)
                    {
                        {|SES1506:app.UseDeveloperExceptionPage()|};
                    }
                }
            }
            """);

    /// <summary>Verifies a call guarded by <c>app.Environment.IsDevelopment()</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnvironmentIsDevelopmentGuardIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app)
                {
                    if (app.Environment.IsDevelopment())
                    {
                        app.UseDeveloperExceptionPage();
                    }
                }
            }
            """);

    /// <summary>Verifies a call guarded by a parameter's <c>env.IsDevelopment()</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterIsDevelopmentGuardIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app, IHostEnvironment env)
                {
                    if (env.IsDevelopment())
                    {
                        app.UseDeveloperExceptionPage();
                    }
                }
            }
            """);

    /// <summary>Verifies a conditional-expression guard using <c>IsDevelopment</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalExpressionGuardIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public IApplicationBuilder M(IApplicationBuilder app, IHostEnvironment env)
                    => env.IsDevelopment() ? app.UseDeveloperExceptionPage() : app;
            }
            """);

    /// <summary>Verifies a null-conditional <c>env?.IsDevelopment()</c> guard is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessGuardIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                public void M(IApplicationBuilder app, IHostEnvironment env)
                {
                    if (env?.IsDevelopment() == true)
                    {
                        app.UseDeveloperExceptionPage();
                    }
                }
            }
            """);

    /// <summary>Verifies an unqualified <c>IsDevelopment()</c> guard is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnqualifiedIsDevelopmentGuardIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public class C
            {
                private static bool IsDevelopment() => true;

                public void M(IApplicationBuilder app)
                {
                    if (IsDevelopment())
                    {
                        app.UseDeveloperExceptionPage();
                    }
                }
            }
            """);

    /// <summary>Verifies a same-named method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMethodOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Builder;

            public sealed class Other
            {
                public void UseDeveloperExceptionPage()
                {
                }
            }

            public class C
            {
                public void M(Other other)
                {
                    other.UseDeveloperExceptionPage();
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the extensions type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenExtensionsTypeUnavailableAsync()
    {
        const string Source = """
                              public interface IApplicationBuilder
                              {
                              }

                              public static class DeveloperExceptionPageExtensions
                              {
                                  public static IApplicationBuilder UseDeveloperExceptionPage(this IApplicationBuilder app) => app;
                              }

                              public class C
                              {
                                  public void M(IApplicationBuilder app)
                                  {
                                      app.UseDeveloperExceptionPage();
                                  }
                              }
                              """;

        var test = new AnalyzeDeveloperPage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core application-builder stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeDeveloperPage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + AspNetStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
