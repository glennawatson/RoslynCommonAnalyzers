// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeCredential = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1202HardcodedCredentialArgumentAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1202 (a non-empty string literal bound to a credential parameter or credential-type constructor).</summary>
public class HardcodedCredentialArgumentAnalyzerUnitTest
{
    /// <summary>Verifies a string literal passed to an <c>apiKey</c> parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralToApiKeyParameterReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Connect(string apiKey) { }

                public void M() => Connect({|SES1202:"k3y-not-a-known-pattern-value"|});
            }
            """);

    /// <summary>Verifies a string literal passed to a <c>password</c> parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralToPasswordParameterReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Login(string user, string password) { }

                public void M() => Login("alice", {|SES1202:"hunter2-literal-value"|});
            }
            """);

    /// <summary>Verifies a string literal passed to an underscore-cased <c>access_token</c> parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralToUnderscoreTokenParameterReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Send(string access_token) { }

                public void M() => Send({|SES1202:"abcDEF123456"|});
            }
            """);

    /// <summary>Verifies matching is case-insensitive on the parameter name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterNameMatchIsCaseInsensitiveAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Use(string ApiKey) { }

                public void M() => Use({|SES1202:"literalkey12345"|});
            }
            """);

    /// <summary>Verifies a named argument bound to a credential parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedCredentialArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Open(string host, string secret) { }

                public void M() => Open(secret: {|SES1202:"top-secret-literal"|}, host: "example.com");
            }
            """);

    /// <summary>Verifies a string literal passed to a <c>connectionString</c> parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralToConnectionStringParameterReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Configure(string connectionString) { }

                public void M() => Configure({|SES1202:"Server=db;User=sa;Password=p"|});
            }
            """);

    /// <summary>Verifies a string literal in the <c>password</c> position of a <c>NetworkCredential</c> constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralNetworkCredentialPasswordReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net;

            public class C
            {
                public NetworkCredential M() => new NetworkCredential("svc-account", {|SES1202:"literal-password-value"|});
            }
            """);

    /// <summary>Verifies a target-typed <c>new(...)</c> credential construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralImplicitNetworkCredentialReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net;

            public class C
            {
                public NetworkCredential M()
                {
                    NetworkCredential c = new("svc-account", {|SES1202:"literal-password-value"|});
                    return c;
                }
            }
            """);

    /// <summary>Verifies a string literal in the <c>key</c> position of a gated <c>AzureKeyCredential</c> is reported even though <c>key</c> is too generic to flag elsewhere.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralAzureKeyCredentialReportedAsync()
        => await VerifyNet90Async(
            """
            namespace Azure
            {
                public sealed class AzureKeyCredential
                {
                    public AzureKeyCredential(string key) { }
                }
            }

            public class C
            {
                public Azure.AzureKeyCredential M() => new Azure.AzureKeyCredential({|SES1202:"literal-azure-key-value"|});
            }
            """);

    /// <summary>Verifies a string literal to the <c>ApiKeyCredential.key</c> position is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralApiKeyCredentialReportedAsync()
        => await VerifyNet90Async(
            """
            namespace System.ClientModel
            {
                public class ApiKeyCredential
                {
                    public ApiKeyCredential(string key) { }
                }
            }

            public class C
            {
                public System.ClientModel.ApiKeyCredential M() => new System.ClientModel.ApiKeyCredential({|SES1202:"literal-client-key"|});
            }
            """);

    /// <summary>Verifies a string literal to the <c>clientSecret</c> position of <c>ClientSecretCredential</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralClientSecretCredentialReportedAsync()
        => await VerifyNet90Async(
            """
            namespace Azure.Identity
            {
                public class ClientSecretCredential
                {
                    public ClientSecretCredential(string tenantId, string clientId, string clientSecret) { }
                }
            }

            public class C
            {
                public Azure.Identity.ClientSecretCredential M()
                    => new Azure.Identity.ClientSecretCredential("tenant", "client", {|SES1202:"literal-client-secret"|});
            }
            """);

    /// <summary>Verifies a variable (non-literal) passed to a credential parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VariableToCredentialParameterIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Connect(string apiKey) { }

                public void M(string apiKey) => Connect(apiKey);
            }
            """);

    /// <summary>Verifies a constant field reference (non-literal) passed to a credential parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantReferenceToCredentialParameterIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string DefaultKey = "abc";

                private static void Connect(string apiKey) { }

                public void M() => Connect(DefaultKey);
            }
            """);

    /// <summary>Verifies an environment read (the correct pattern) passed to a credential parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnvironmentReadToCredentialParameterIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                private static void Connect(string apiKey) { }

                public void M() => Connect(Environment.GetEnvironmentVariable("API_KEY"));
            }
            """);

    /// <summary>Verifies a string literal passed to a non-credential parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralToNonCredentialParameterIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Log(string message) { }

                public void M() => Log("connecting to the database now");
            }
            """);

    /// <summary>Verifies a string literal bound to a bare <c>key</c> parameter outside a gated credential type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralToBareKeyParameterOutsideCredentialTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(Dictionary<string, int> map) => map.Add("some-key-name", 1);
            }
            """);

    /// <summary>Verifies the non-secret <c>userName</c> position of a <c>NetworkCredential</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NetworkCredentialUserNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Net;

            public class C
            {
                public NetworkCredential M(string password) => new NetworkCredential("admin", password);
            }
            """);

    /// <summary>Verifies an empty string password is not reported (an empty credential is a different concern).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyStringCredentialIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Login(string password) { }

                public void M() => Login("");
            }
            """);

    /// <summary>Verifies a <c>your-...</c> placeholder credential is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task YourPlaceholderCredentialIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Connect(string apiKey) { }

                public void M() => Connect("your-api-key-here");
            }
            """);

    /// <summary>Verifies an angle-bracket placeholder credential is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AngleBracketPlaceholderCredentialIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Connect(string apiKey) { }

                public void M() => Connect("<insert-key-here>");
            }
            """);

    /// <summary>Verifies a <c>changeme</c> placeholder credential is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChangeMePlaceholderCredentialIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Login(string password) { }

                public void M() => Login("changeme");
            }
            """);

    /// <summary>Verifies an all-same-character (masked) placeholder credential is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllSameCharacterPlaceholderCredentialIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static void Login(string password) { }

                public void M() => Login("xxxxxxxx");
            }
            """);

    /// <summary>Verifies a literal bound to a bare <c>key</c> parameter on a non-credential type is not reported (the gated position applies only to known credential types).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareKeyParameterSilentWhenCredentialTypeAbsentAsync()
    {
        const string Source = """
                              public class Signer
                              {
                                  public Signer(string key) { }
                              }

                              public class C
                              {
                                  public Signer M() => new Signer("literal-key-value-here");
                              }
                              """;

        var test = new AnalyzeCredential.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeCredential.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
