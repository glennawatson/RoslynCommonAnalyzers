// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeValidator = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1102AcceptAnyServerCertificateAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1102 (do not read the accept-any server-certificate validator).</summary>
public class AcceptAnyServerCertificateAnalyzerUnitTest
{
    /// <summary>Verifies assigning the validator to the custom-validation callback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignedToValidationCallbackReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;

            public class C
            {
                public HttpClient Make()
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = {|SES1102:HttpClientHandler.DangerousAcceptAnyServerCertificateValidator|};
                    return new HttpClient(handler);
                }
            }
            """);

    /// <summary>Verifies a bare read of the validator into a local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareReadReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;

            public class C
            {
                public object Read()
                {
                    var validator = {|SES1102:HttpClientHandler.DangerousAcceptAnyServerCertificateValidator|};
                    return validator;
                }
            }
            """);

    /// <summary>Verifies a fully qualified read of the validator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedReadReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public object Read()
                    => {|SES1102:System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator|};
            }
            """);

    /// <summary>Verifies a genuine custom validation callback that inspects the errors is not reported.</summary>
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

    /// <summary>Verifies a same-named member on an unrelated type is not reported (binding, not text).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMemberOnUnrelatedTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class HttpClientHandler
            {
                public static object DangerousAcceptAnyServerCertificateValidator => null!;
            }

            public class C
            {
                public object Read() => HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework whose handler lacks the member (net472).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenHandlerMemberUnavailableAsync()
    {
        const string Source = """
                              public sealed class HttpClientHandler
                              {
                                  public static object DangerousAcceptAnyServerCertificateValidator => null!;
                              }

                              public class C
                              {
                                  public object Read() => HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                              }
                              """;

        var test = new AnalyzeValidator.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the member exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeValidator.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
