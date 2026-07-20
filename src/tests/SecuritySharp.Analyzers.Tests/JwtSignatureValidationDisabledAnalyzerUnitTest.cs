// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeSignature = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1503JwtSignatureValidationDisabledAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1503 (JWT signature verification disabled on TokenValidationParameters).</summary>
public class JwtSignatureValidationDisabledAnalyzerUnitTest
{
    /// <summary>Inline stub of the token-validation options type carrying the signature and out-of-scope flags.</summary>
    private const string TokenValidationStub = """

                                               namespace Microsoft.IdentityModel.Tokens
                                               {
                                                   public class TokenValidationParameters
                                                   {
                                                       public bool RequireSignedTokens { get; set; }

                                                       public bool ValidateIssuerSigningKey { get; set; }

                                                       public bool ValidateIssuer { get; set; }

                                                       public bool ValidateAudience { get; set; }

                                                       public bool ValidateLifetime { get; set; }
                                                   }
                                               }
                                               """;

    /// <summary>Verifies a plain <c>RequireSignedTokens = false</c> assignment is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequireSignedTokensAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Tokens;

            public class C
            {
                public void M(TokenValidationParameters parameters)
                {
                    {|SES1503:parameters.RequireSignedTokens = false|};
                }
            }
            """);

    /// <summary>Verifies a plain <c>ValidateIssuerSigningKey = false</c> assignment is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidateIssuerSigningKeyAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Tokens;

            public class C
            {
                public void M(TokenValidationParameters parameters)
                {
                    {|SES1503:parameters.ValidateIssuerSigningKey = false|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form of <c>RequireSignedTokens = false</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequireSignedTokensObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Tokens;

            public class C
            {
                public TokenValidationParameters M()
                    => new TokenValidationParameters { {|SES1503:RequireSignedTokens = false|} };
            }
            """);

    /// <summary>Verifies the object-initializer form of <c>ValidateIssuerSigningKey = false</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidateIssuerSigningKeyObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Tokens;

            public class C
            {
                public TokenValidationParameters M()
                    => new TokenValidationParameters { {|SES1503:ValidateIssuerSigningKey = false|} };
            }
            """);

    /// <summary>Verifies setting <c>RequireSignedTokens</c> to true is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequireSignedTokensSetToTrueIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Tokens;

            public class C
            {
                public void M(TokenValidationParameters parameters)
                {
                    parameters.RequireSignedTokens = true;
                }
            }
            """);

    /// <summary>Verifies <c>ValidateIssuer = false</c> is not reported (a separate concern handled elsewhere).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidateIssuerIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Tokens;

            public class C
            {
                public void M(TokenValidationParameters parameters)
                {
                    parameters.ValidateIssuer = false;
                }
            }
            """);

    /// <summary>Verifies <c>ValidateAudience = false</c> and <c>ValidateLifetime = false</c> are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidateAudienceAndLifetimeAreCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.IdentityModel.Tokens;

            public class C
            {
                public void M(TokenValidationParameters parameters)
                {
                    parameters.ValidateAudience = false;
                    parameters.ValidateLifetime = false;
                }
            }
            """);

    /// <summary>Verifies a same-named property on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public sealed class MyOptions
            {
                public bool RequireSignedTokens { get; set; }

                public bool ValidateIssuerSigningKey { get; set; }
            }

            public class C
            {
                public void M(MyOptions options)
                {
                    options.RequireSignedTokens = false;
                    options.ValidateIssuerSigningKey = false;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the options type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenOptionsTypeUnavailableAsync()
    {
        const string Source = """
                              public sealed class TokenValidationParameters
                              {
                                  public bool RequireSignedTokens { get; set; }

                                  public bool ValidateIssuerSigningKey { get; set; }
                              }

                              public class C
                              {
                                  public void M(TokenValidationParameters parameters)
                                  {
                                      parameters.RequireSignedTokens = false;
                                      parameters.ValidateIssuerSigningKey = false;
                                  }
                              }
                              """;

        var test = new AnalyzeSignature.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline TokenValidationParameters stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeSignature.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + TokenValidationStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
