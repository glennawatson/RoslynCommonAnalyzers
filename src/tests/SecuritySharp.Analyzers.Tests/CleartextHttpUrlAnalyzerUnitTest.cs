// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeUrl = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1106CleartextHttpUrlAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1106 (an HttpClient request must not target a cleartext http URL literal).</summary>
public class CleartextHttpUrlAnalyzerUnitTest
{
    /// <summary>Verifies a cleartext string URL passed to GetAsync is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringUrlToGetAsyncReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync({|SES1106:"http://example.com/api"|});
                }
            }
            """);

    /// <summary>Verifies a cleartext string URL passed to PostAsync is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringUrlToPostAsyncReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client, HttpContent content)
                {
                    await client.PostAsync({|SES1106:"http://example.com/p"|}, content);
                }
            }
            """);

    /// <summary>Verifies a cleartext URL passed by the requestUri: name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedRequestUriArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync(requestUri: {|SES1106:"http://example.com/api"|});
                }
            }
            """);

    /// <summary>Verifies a cleartext new Uri(...) argument to a request method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewUriArgumentToGetAsyncReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync(new Uri({|SES1106:"http://example.com/api"|}));
                }
            }
            """);

    /// <summary>Verifies a cleartext new Uri(...) assigned to BaseAddress is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseAddressAssignmentReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Net.Http;

            public class C
            {
                public void M(HttpClient client)
                {
                    client.BaseAddress = new Uri({|SES1106:"http://example.com"|});
                }
            }
            """);

    /// <summary>Verifies a bare <c>BaseAddress = new Uri(uriString: …)</c> inside an HttpClient subclass is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubclassBareBaseAddressWithNamedUriStringReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Net.Http;

            public class C : HttpClient
            {
                public void M()
                {
                    BaseAddress = new Uri(uriString: {|SES1106:"http://example.com"|});
                }
            }
            """);

    /// <summary>Verifies a verbatim cleartext string URL is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerbatimStringUrlReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync({|SES1106:@"http://example.com/api"|});
                }
            }
            """);

    /// <summary>Verifies a raw cleartext string URL is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RawStringUrlReportedAsync()
        => await VerifyNet90Async(
            """"
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync({|SES1106:"""http://example.com/api"""|});
                }
            }
            """");

    /// <summary>Verifies a mixed-case HTTP scheme is reported (the scheme match is case-insensitive).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedCaseSchemeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync({|SES1106:"HTTP://example.com/api"|});
                }
            }
            """);

    /// <summary>Verifies an https URL is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HttpsUrlIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync("https://example.com/api");
                }
            }
            """);

    /// <summary>Verifies a cleartext localhost URL is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalhostUrlIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync("http://localhost:5000/api");
                    client.BaseAddress = new Uri("http://127.0.0.1");
                    await client.GetAsync("http://[::1]:8080/health");
                    await client.GetAsync("http://api.localhost/data");
                    await client.GetAsync("http://[::1");
                }
            }
            """);

    /// <summary>Verifies a cleartext URL with no host (empty authority) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyHostUrlIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetAsync("http:///only-a-path");
                }
            }
            """);

    /// <summary>Verifies HttpClient calls and assignments that are not URL sinks are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSinkShapesAreCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client, Uri existing, int number)
                {
                    // A member call whose name is not a guarded request method.
                    client.CancelPendingRequests();

                    // A non-member (local function) invocation.
                    Local();

                    // A request whose only argument is named something other than 'requestUri'.
                    await client.SendAsync(request: new HttpRequestMessage());

                    // A cleartext URL through 'new Uri(null)' has no string literal to flag.
                    client.BaseAddress = new Uri(null);

                    // A 'new Uri(...)' whose leading argument is named something other than 'uriString'.
                    client.BaseAddress = new Uri(baseUri: existing, relativeUri: "path");

                    // A member assignment whose target is not BaseAddress.
                    client.Timeout = TimeSpan.Zero;

                    // A bare identifier assignment whose target is not BaseAddress.
                    number = 1;

                    // BaseAddress assigned from an existing Uri (not a 'new Uri(...)' literal).
                    client.BaseAddress = existing;

                    await Task.CompletedTask;

                    void Local()
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a URL held in a variable (not a literal) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VariableUrlIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client, string url)
                {
                    await client.GetAsync(url);
                }
            }
            """);

    /// <summary>Verifies an interpolated URL string is not reported (only literals are tracked).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedUrlIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client, string host)
                {
                    await client.GetAsync($"http://{host}/api");
                }
            }
            """);

    /// <summary>Verifies a cleartext URL to a same-named method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedGetAsyncIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public sealed class FakeClient
            {
                public Task GetAsync(string requestUri) => Task.CompletedTask;
            }

            public class C
            {
                public async Task M()
                {
                    var client = new FakeClient();
                    await client.GetAsync("http://example.com/api");
                }
            }
            """);

    /// <summary>Verifies a cleartext new Uri(...) assigned to an unrelated BaseAddress member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedBaseAddressIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public sealed class FakeClient
            {
                public Uri BaseAddress { get; set; }
            }

            public class C
            {
                public void M()
                {
                    var client = new FakeClient();
                    client.BaseAddress = new Uri("http://example.com");
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when HttpClient does not resolve (netstandard1.0 lacks the type).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenHttpClientUnavailableAsync()
    {
        const string Source = """
                              public sealed class HttpClient
                              {
                                  public void GetAsync(string requestUri)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M()
                                  {
                                      var client = new HttpClient();
                                      client.GetAsync("http://example.com/api");
                                  }
                              }
                              """;

        var test = new AnalyzeUrl.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard10,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where HttpClient exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeUrl.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
