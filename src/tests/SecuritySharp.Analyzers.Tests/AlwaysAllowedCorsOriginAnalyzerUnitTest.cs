// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeCors = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1502AlwaysAllowedCorsOriginAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1502 (a CORS origin predicate must not unconditionally allow every origin).</summary>
public class AlwaysAllowedCorsOriginAnalyzerUnitTest
{
    /// <summary>An inline stub of the ASP.NET Core CORS policy builder with the predicate-taking method.</summary>
    private const string CorsStub = """

                                    namespace Microsoft.AspNetCore.Cors.Infrastructure
                                    {
                                        public class CorsPolicyBuilder
                                        {
                                            public CorsPolicyBuilder SetIsOriginAllowed(System.Func<string, bool> isOriginAllowed) => this;
                                        }
                                    }
                                    """;

    /// <summary>Verifies a discard-parameter <c>_ =&gt; true</c> predicate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardExpressionLambdaReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:_ => true|});
                }
            }
            """);

    /// <summary>Verifies a named-parameter <c>origin =&gt; true</c> predicate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedExpressionLambdaReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:origin => true|});
                }
            }
            """);

    /// <summary>Verifies a parenthesized-parameter lambda with a parenthesized <c>true</c> body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedLambdaReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:(string origin) => (true)|});
                }
            }
            """);

    /// <summary>Verifies a block lambda whose only result is <c>return true;</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockLambdaReturnTrueReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:origin =>
                    {
                        return true;
                    }|});
                }
            }
            """);

    /// <summary>Verifies a block lambda that does work before an unconditional <c>return true;</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockLambdaWithLeadingStatementsReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;
            using System;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:origin =>
                    {
                        Console.WriteLine(origin);
                        return true;
                    }|});
                }
            }
            """);

    /// <summary>Verifies an anonymous method that always returns true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnonymousMethodReturnTrueReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:delegate (string origin)
                    {
                        return true;
                    }|});
                }
            }
            """);

    /// <summary>Verifies a method group to an always-true method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupToAlwaysTrueReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                private static bool AllowAll(string origin) => true;

                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:AllowAll|});
                }
            }
            """);

    /// <summary>Verifies a method group to a block method whose only result is <c>return true;</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupToBlockReturnTrueReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                private static bool AllowAll(string origin)
                {
                    return true;
                }

                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed({|SES1502:AllowAll|});
                }
            }
            """);

    /// <summary>Verifies a local-function method group to an always-true function is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionMethodGroupReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    static bool AllowAll(string origin) => true;
                    builder.SetIsOriginAllowed({|SES1502:AllowAll|});
                }
            }
            """);

    /// <summary>Verifies an expression lambda that inspects the origin is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OriginCheckingLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed(origin => origin.StartsWith("https://trusted"));
                }
            }
            """);

    /// <summary>Verifies a block lambda with a conditional <c>return false;</c> path is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockLambdaWithFalsePathIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed(origin =>
                    {
                        if (origin.Length == 0)
                        {
                            return false;
                        }

                        return true;
                    });
                }
            }
            """);

    /// <summary>Verifies a method group to a method that inspects the origin is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupToOriginCheckIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                private static bool CheckOrigin(string origin) => origin.Length > 0;

                public void M(CorsPolicyBuilder builder)
                {
                    builder.SetIsOriginAllowed(CheckOrigin);
                }
            }
            """);

    /// <summary>Verifies an always-true predicate on a same-named method of an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMethodOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public sealed class MyBuilder
            {
                public MyBuilder SetIsOriginAllowed(Func<string, bool> isOriginAllowed) => this;
            }

            public class C
            {
                public void M(MyBuilder builder)
                {
                    builder.SetIsOriginAllowed(_ => true);
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the CORS policy-builder type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenBuilderTypeUnavailableAsync()
    {
        const string Source = """
                              using System;

                              namespace Other.Cors
                              {
                                  public class CorsPolicyBuilder
                                  {
                                      public CorsPolicyBuilder SetIsOriginAllowed(Func<string, bool> isOriginAllowed) => this;
                                  }
                              }

                              public class C
                              {
                                  public void M(Other.Cors.CorsPolicyBuilder builder)
                                  {
                                      builder.SetIsOriginAllowed(_ => true);
                                  }
                              }
                              """;

        var test = new AnalyzeCors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core CORS stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeCors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + CorsStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
