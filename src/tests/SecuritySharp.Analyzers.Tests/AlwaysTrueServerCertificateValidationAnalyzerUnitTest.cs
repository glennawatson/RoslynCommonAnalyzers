// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeCallback = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1108AlwaysTrueServerCertificateValidationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1108 (a custom server-certificate callback that always accepts the certificate).</summary>
public class AlwaysTrueServerCertificateValidationAnalyzerUnitTest
{
    /// <summary>Verifies an expression lambda that always returns true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionLambdaAlwaysTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = {|SES1108:(message, cert, chain, errors) => true|};
                    return new HttpClient(handler);
                }
            }
            """);

    /// <summary>Verifies a block lambda whose only result is <c>return true;</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockLambdaAlwaysTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = {|SES1108:(message, cert, chain, errors) =>
                    {
                        return true;
                    }|};
                    return new HttpClient(handler);
                }
            }
            """);

    /// <summary>Verifies an always-true callback set through an object initializer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerAlwaysTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = {|SES1108:(message, cert, chain, errors) => true|},
                    };
                    return new HttpClient(handler);
                }
            }
            """);

    /// <summary>Verifies a method group to a source method that always returns true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupAlwaysTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Net.Security;
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = {|SES1108:AlwaysValid|};
                    return new HttpClient(handler);
                }

                private static bool AlwaysValid(HttpRequestMessage message, X509Certificate2 cert, X509Chain chain, SslPolicyErrors errors) => true;
            }
            """);

    /// <summary>Verifies a callback that actually inspects the certificate errors is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RealValidationCallbackIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Net.Security;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback =
                        (message, cert, chain, errors) => errors == SslPolicyErrors.None;
                    return new HttpClient(handler);
                }
            }
            """);

    /// <summary>Verifies a method group to a source method that really validates is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RealMethodGroupIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Net.Security;
            using System.Security.Cryptography.X509Certificates;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = Validate;
                    return new HttpClient(handler);
                }

                private static bool Validate(HttpRequestMessage message, X509Certificate2 cert, X509Chain chain, SslPolicyErrors errors)
                    => errors == SslPolicyErrors.None;
            }
            """);

    /// <summary>Verifies the built-in accept-any sentinel is not reported here — its own rule owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BuiltInSentinelIsNotReportedHereAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    return new HttpClient(handler);
                }
            }
            """);

    /// <summary>Verifies a same-named callback property on an unrelated type is not reported (binding, not text).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyOnUnrelatedTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public sealed class HttpClientHandler
            {
                public Func<object, object, object, int, bool> ServerCertificateCustomValidationCallback { get; set; }
            }

            public class C
            {
                public void Configure()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the property exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeCallback.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
