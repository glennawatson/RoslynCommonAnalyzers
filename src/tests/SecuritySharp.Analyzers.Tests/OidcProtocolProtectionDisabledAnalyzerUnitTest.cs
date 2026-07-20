// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeProtections = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1514OidcProtocolProtectionDisabledAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1514 (OpenID Connect PKCE, state, and nonce protections must not be disabled).</summary>
public class OidcProtocolProtectionDisabledAnalyzerUnitTest
{
    /// <summary>Inline stubs of the OpenID Connect options and protocol-validator types carrying the guarded flags.</summary>
    private const string OidcStub = """

                                    namespace Microsoft.IdentityModel.Protocols.OpenIdConnect
                                    {
                                        public class OpenIdConnectProtocolValidator
                                        {
                                            public bool RequireState { get; set; }

                                            public bool RequireStateValidation { get; set; }

                                            public bool RequireNonce { get; set; }
                                        }
                                    }

                                    namespace Microsoft.AspNetCore.Authentication.OpenIdConnect
                                    {
                                        public class OpenIdConnectOptions
                                        {
                                            public bool UsePkce { get; set; }

                                            public Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectProtocolValidator ProtocolValidator { get; set; }
                                                = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectProtocolValidator();
                                        }
                                    }
                                    """;

    /// <summary>Verifies a direct <c>UsePkce = false</c> assignment is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsePkceAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;

            public class C
            {
                public void M(OpenIdConnectOptions options)
                {
                    {|SES1514:options.UsePkce = false|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form of <c>UsePkce = false</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsePkceObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;

            public class C
            {
                public OpenIdConnectOptions M()
                    => new OpenIdConnectOptions { {|SES1514:UsePkce = false|} };
            }
            """);

    /// <summary>Verifies a direct <c>RequireState = false</c> assignment on the validator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequireStateAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;

            public class C
            {
                public void M(OpenIdConnectOptions options)
                {
                    {|SES1514:options.ProtocolValidator.RequireState = false|};
                }
            }
            """);

    /// <summary>Verifies a direct <c>RequireStateValidation = false</c> assignment on the validator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequireStateValidationAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;

            public class C
            {
                public void M(OpenIdConnectOptions options)
                {
                    {|SES1514:options.ProtocolValidator.RequireStateValidation = false|};
                }
            }
            """);

    /// <summary>Verifies a direct <c>RequireNonce = false</c> assignment on the validator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequireNonceAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;

            public class C
            {
                public void M(OpenIdConnectOptions options)
                {
                    {|SES1514:options.ProtocolValidator.RequireNonce = false|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form of <c>RequireNonce = false</c> on the validator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequireNonceObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Protocols.OpenIdConnect;

            public class C
            {
                public OpenIdConnectProtocolValidator M()
                    => new OpenIdConnectProtocolValidator { {|SES1514:RequireNonce = false|} };
            }
            """);

    /// <summary>Verifies setting <c>UsePkce</c> to true is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsePkceSetToTrueIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;

            public class C
            {
                public void M(OpenIdConnectOptions options)
                {
                    options.UsePkce = true;
                    options.ProtocolValidator.RequireNonce = true;
                }
            }
            """);

    /// <summary>Verifies same-named flags on an unrelated type, and unrelated <c>= false</c> assignments, are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyAndUnrelatedFalseAssignmentsAreCleanAsync()
        => await VerifyAsync(
            """
            public sealed class MyOptions
            {
                public bool UsePkce { get; set; }

                public bool RequireState { get; set; }

                public bool RequireNonce { get; set; }
            }

            public class C
            {
                public void M(MyOptions options, bool[] flags, bool enabled)
                {
                    bool RequireNonce = true;
                    RequireNonce = false;
                    options.UsePkce = false;
                    options.RequireState = false;
                    options.RequireNonce = false;
                    enabled = false;
                    flags[0] = false;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the OpenID Connect options type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenOptionsTypeUnavailableAsync()
    {
        const string Source = """
                              public sealed class OpenIdConnectOptions
                              {
                                  public bool UsePkce { get; set; }
                              }

                              public class C
                              {
                                  public void M(OpenIdConnectOptions options)
                                  {
                                      options.UsePkce = false;
                                  }
                              }
                              """;

        var test = new AnalyzeProtections.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline OpenID Connect stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeProtections.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + OidcStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
