// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeCors = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1501CorsAnyOriginWithCredentialsAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1501 (a CORS policy must not allow credentials together with any origin).</summary>
public class CorsAnyOriginWithCredentialsAnalyzerUnitTest
{
    /// <summary>Inline stubs of the ASP.NET Core CORS policy-builder and options types.</summary>
    private const string CorsStubs = """

                                     namespace Microsoft.AspNetCore.Cors.Infrastructure
                                     {
                                         public class CorsPolicyBuilder
                                         {
                                             public CorsPolicyBuilder AllowAnyOrigin() => this;

                                             public CorsPolicyBuilder AllowCredentials() => this;

                                             public CorsPolicyBuilder AllowAnyHeader() => this;

                                             public CorsPolicyBuilder AllowAnyMethod() => this;

                                             public CorsPolicyBuilder WithOrigins(params string[] origins) => this;
                                         }

                                         public class CorsOptions
                                         {
                                             public void AddPolicy(string name, System.Action<CorsPolicyBuilder> configurePolicy) => configurePolicy(new CorsPolicyBuilder());

                                             public void AddDefaultPolicy(System.Action<CorsPolicyBuilder> configurePolicy) => configurePolicy(new CorsPolicyBuilder());
                                         }
                                     }
                                     """;

    /// <summary>Verifies a fluent chain that allows any origin and then credentials is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnyOriginThenCredentialsChainReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsOptions options)
                    => options.AddPolicy("open", policy => policy.AllowAnyOrigin().{|SES1501:AllowCredentials()|});
            }
            """);

    /// <summary>Verifies a reversed chain (credentials then any origin) is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CredentialsThenAnyOriginChainReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsOptions options)
                    => options.AddPolicy("open", policy => policy.{|SES1501:AllowCredentials()|}.AllowAnyOrigin());
            }
            """);

    /// <summary>Verifies the two calls in separate statements of one policy lambda body are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockLambdaSeparateStatementsReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsOptions options)
                {
                    options.AddPolicy("open", policy =>
                    {
                        policy.AllowAnyOrigin();
                        policy.AllowAnyHeader();
                        policy.{|SES1501:AllowCredentials()|};
                    });
                }
            }
            """);

    /// <summary>Verifies the combination inside an <c>AddDefaultPolicy</c> lambda is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddDefaultPolicyChainReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsOptions options)
                    => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().{|SES1501:AllowCredentials()|});
            }
            """);

    /// <summary>Verifies a bare fluent chain not inside a policy lambda is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareFluentChainReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public CorsPolicyBuilder M()
                    => new CorsPolicyBuilder().AllowAnyOrigin().{|SES1501:AllowCredentials()|};
            }
            """);

    /// <summary>Verifies allowing any origin without credentials is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnyOriginWithoutCredentialsIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsOptions options)
                    => options.AddPolicy("open", policy => policy.AllowAnyOrigin().AllowAnyHeader());
            }
            """);

    /// <summary>Verifies allowing credentials for specific origins is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CredentialsWithSpecificOriginsIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsOptions options)
                    => options.AddPolicy("trusted", policy => policy.WithOrigins("https://trusted.example").AllowCredentials());
            }
            """);

    /// <summary>Verifies two separate policies -- one any-origin, one credentials -- are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatePoliciesAreCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsOptions options)
                {
                    options.AddPolicy("public", policy => policy.AllowAnyOrigin());
                    options.AddPolicy("trusted", policy => policy.WithOrigins("https://trusted.example").AllowCredentials());
                }
            }
            """);

    /// <summary>Verifies the two calls in separate non-lambda statements are not reported (local scope only).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparateNonLambdaStatementsAreCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public class C
            {
                public void M(CorsPolicyBuilder policy)
                {
                    policy.AllowAnyOrigin();
                    policy.AllowCredentials();
                }
            }
            """);

    /// <summary>Verifies same-named methods on an unrelated builder type are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMethodsOnUnrelatedTypeAreCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Cors.Infrastructure;

            public sealed class OtherBuilder
            {
                public OtherBuilder AllowAnyOrigin() => this;

                public OtherBuilder AllowCredentials() => this;
            }

            public class C
            {
                public void M(OtherBuilder builder)
                    => builder.AllowAnyOrigin().AllowCredentials();
            }
            """);

    /// <summary>Verifies the rule stays silent when the CORS policy-builder type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenBuilderUnavailableAsync()
    {
        const string Source = """
                              public sealed class CorsPolicyBuilder
                              {
                                  public CorsPolicyBuilder AllowAnyOrigin() => this;

                                  public CorsPolicyBuilder AllowCredentials() => this;
                              }

                              public class C
                              {
                                  public CorsPolicyBuilder M()
                                      => new CorsPolicyBuilder().AllowAnyOrigin().AllowCredentials();
                              }
                              """;

        var test = new AnalyzeCors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core CORS stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeCors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + CorsStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
