// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeMetadata = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1105PlainHttpMetadataRetrievalAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1105 (bearer/OpenID metadata retrieval over plain HTTP without a development guard).</summary>
public class PlainHttpMetadataRetrievalAnalyzerUnitTest
{
    /// <summary>Inline stubs of the ASP.NET Core authentication option types and a development-environment probe.</summary>
    private const string AspNetStubs = """

                                       namespace Microsoft.AspNetCore.Authentication.JwtBearer
                                       {
                                           public class JwtBearerOptions
                                           {
                                               public bool RequireHttpsMetadata { get; set; }

                                               public bool SaveToken { get; set; }
                                           }
                                       }

                                       namespace Microsoft.AspNetCore.Authentication.OpenIdConnect
                                       {
                                           public class OpenIdConnectOptions
                                           {
                                               public bool RequireHttpsMetadata { get; set; }
                                           }
                                       }

                                       public sealed class HostEnvironment
                                       {
                                           public HostEnvironment Environment => this;

                                           public bool IsDevelopment() => true;
                                       }
                                       """;

    /// <summary>Verifies a plain <c>RequireHttpsMetadata = false</c> on JwtBearerOptions is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JwtBearerAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public class C
            {
                public void M(JwtBearerOptions options)
                {
                    {|SES1105:options.RequireHttpsMetadata = false|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form on JwtBearerOptions is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JwtBearerObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public class C
            {
                public JwtBearerOptions M()
                    => new JwtBearerOptions { {|SES1105:RequireHttpsMetadata = false|} };
            }
            """);

    /// <summary>Verifies a plain <c>RequireHttpsMetadata = false</c> on OpenIdConnectOptions is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OpenIdConnectAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;

            public class C
            {
                public void M(OpenIdConnectOptions options)
                {
                    {|SES1105:options.RequireHttpsMetadata = false|};
                }
            }
            """);

    /// <summary>Verifies an assignment guarded by <c>env.IsDevelopment()</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DevelopmentGuardedAssignmentIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public class C
            {
                public void M(JwtBearerOptions options, HostEnvironment env)
                {
                    if (env.IsDevelopment())
                    {
                        options.RequireHttpsMetadata = false;
                    }
                }
            }
            """);

    /// <summary>Verifies a chained <c>builder.Environment.IsDevelopment()</c> guard is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedEnvironmentGuardIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public class C
            {
                public void M(JwtBearerOptions options, HostEnvironment builder)
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        options.RequireHttpsMetadata = false;
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
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public class C
            {
                public void M(JwtBearerOptions options, HostEnvironment env)
                {
                    var applied = env.IsDevelopment() ? (options.RequireHttpsMetadata = false) : true;
                }
            }
            """);

    /// <summary>Verifies setting <c>RequireHttpsMetadata</c> to true is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentToTrueIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public class C
            {
                public void M(JwtBearerOptions options)
                {
                    options.RequireHttpsMetadata = true;
                }
            }
            """);

    /// <summary>Verifies setting an unrelated option property to false is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedPropertyIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public class C
            {
                public void M(JwtBearerOptions options)
                {
                    options.SaveToken = false;
                }
            }
            """);

    /// <summary>Verifies a same-named property on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.JwtBearer;

            public sealed class MyOptions
            {
                public bool RequireHttpsMetadata { get; set; }
            }

            public class C
            {
                public void M(MyOptions options)
                {
                    options.RequireHttpsMetadata = false;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the option types are absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenOptionTypesUnavailableAsync()
    {
        const string Source = """
                              public sealed class JwtBearerOptions
                              {
                                  public bool RequireHttpsMetadata { get; set; }
                              }

                              public class C
                              {
                                  public void M(JwtBearerOptions options)
                                  {
                                      options.RequireHttpsMetadata = false;
                                  }
                              }
                              """;

        var test = new AnalyzeMetadata.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core option-type stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeMetadata.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + AspNetStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
