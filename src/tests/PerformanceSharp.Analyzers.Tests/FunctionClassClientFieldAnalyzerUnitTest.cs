// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1420FunctionClassClientFieldAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1420FunctionClassClientFieldAnalyzer"/> (PSH1420 per-invocation client fields).</summary>
public class FunctionClassClientFieldAnalyzerUnitTest
{
    /// <summary>A minimal isolated-worker function attribute declared under its real namespace, so the metadata-name gate resolves.</summary>
    private const string FunctionAttributeStubSource = """
        namespace Microsoft.Azure.Functions.Worker
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class FunctionAttribute : System.Attribute
            {
                public FunctionAttribute(string name) => Name = name;

                public string Name { get; }
            }
        }
        """;

    /// <summary>Minimal service-client surfaces declared under their real SDK namespaces, so the metadata-name probes resolve.</summary>
    private const string ServiceClientStubsSource = """
        namespace Azure.Storage.Blobs
        {
            public class BlobContainerClient
            {
                public System.Threading.Tasks.Task CreateIfNotExistsAsync() => System.Threading.Tasks.Task.CompletedTask;
            }

            public class BlobServiceClient
            {
                public BlobServiceClient(string connectionString)
                {
                }

                public BlobContainerClient GetBlobContainerClient(string name) => new BlobContainerClient();
            }
        }

        namespace Microsoft.Azure.Cosmos
        {
            public class CosmosClient : System.IDisposable
            {
                public CosmosClient(string connectionString)
                {
                }

                public void Dispose()
                {
                }
            }
        }
        """;

    /// <summary>Verifies an instance HttpClient field of a function class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceHttpClientFieldIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            using Microsoft.Azure.Functions.Worker;

            public class ImageFunctions
            {
                private readonly HttpClient {|PSH1420:_client|} = new HttpClient();

                [Function("Run")]
                public Task<string> Run(string url) => _client.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies an injected instance HttpClient field of a function class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedInstanceHttpClientFieldIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            using Microsoft.Azure.Functions.Worker;

            public class ImageFunctions
            {
                private readonly HttpClient {|PSH1420:_client|};

                public ImageFunctions(HttpClient client) => _client = client;

                [Function("Run")]
                public Task<string> Run(string url) => _client.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies an instance HttpClient auto-property of a function class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceHttpClientAutoPropertyIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            using Microsoft.Azure.Functions.Worker;

            public class ImageFunctions
            {
                public HttpClient {|PSH1420:Client|} { get; } = new HttpClient();

                [Function("Run")]
                public Task<string> Run(string url) => Client.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies the fully-qualified function attribute form still resolves the gate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedFunctionAttributeIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class ImageFunctions
            {
                private readonly HttpClient {|PSH1420:_client|} = new HttpClient();

                [Microsoft.Azure.Functions.Worker.Function("Run")]
                public Task<string> Run(string url) => _client.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies a record function class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordFunctionClassIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            using Microsoft.Azure.Functions.Worker;

            public record ImageFunctions
            {
                private readonly HttpClient {|PSH1420:_client|} = new HttpClient();

                [Function("Run")]
                public Task<string> Run(string url) => _client.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies an injected service-client field of a function class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceServiceClientFieldIsFlaggedAsync()
        => await VerifyWithServiceClientsAsync(
            """
            using Microsoft.Azure.Cosmos;
            using Microsoft.Azure.Functions.Worker;

            public class ImageFunctions
            {
                private readonly CosmosClient {|PSH1420:_client|};

                public ImageFunctions(CosmosClient client) => _client = client;

                [Function("Run")]
                public CosmosClient Run() => _client;
            }
            """);

    /// <summary>Verifies a static client field is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticClientFieldIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            using Microsoft.Azure.Functions.Worker;

            public class ImageFunctions
            {
                private static readonly HttpClient Client = new HttpClient();

                [Function("Run")]
                public Task<string> Run(string url) => Client.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies a computed property and a static field are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputedPropertyIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;
            using Microsoft.Azure.Functions.Worker;

            public class ImageFunctions
            {
                private static readonly HttpClient Shared = new HttpClient();

                public HttpClient Client => Shared;

                [Function("Run")]
                public Task<string> Run(string url) => Client.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies a client field in a class with no function method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonFunctionClassIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class Service
            {
                private readonly HttpClient _client = new HttpClient();

                public HttpClient Get() => _client;
            }
            """);

    /// <summary>Verifies a non-client instance field in a function class is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonClientFieldIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.Azure.Functions.Worker;

            public class ImageFunctions
            {
                private readonly string _name = "images";

                [Function("Run")]
                public string Run() => _name;
            }
            """);

    /// <summary>Verifies a client field is never reported when the worker attribute is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClientFieldWithoutWorkerAttributeIsCleanAsync()
        => await VerifyWithoutWorkerAttributeAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            namespace Sample
            {
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class FunctionAttribute : System.Attribute
                {
                    public FunctionAttribute(string name)
                    {
                    }
                }

                public class ImageFunctions
                {
                    private readonly HttpClient _client = new HttpClient();

                    [Function("Run")]
                    public Task<string> Run(string url) => _client.GetStringAsync(url);
                }
            }
            """);

    /// <summary>Verifies a same-named attribute that is not the worker attribute is never treated as a function.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeignFunctionAttributeIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            namespace Other
            {
                public sealed class FunctionAttribute : System.Attribute
                {
                }
            }

            public class ImageFunctions
            {
                private readonly HttpClient _client = new HttpClient();

                [Other.Function]
                public Task<string> Run(string url) => _client.GetStringAsync(url);
            }
            """);

    /// <summary>Runs a verification with the worker function attribute stub added to the compilation.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.Sources.Add(FunctionAttributeStubSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification with the worker attribute and service-client stub surfaces added to the compilation.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithServiceClientsAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.Sources.Add(FunctionAttributeStubSource);
        test.TestState.Sources.Add(ServiceClientStubsSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification without the worker function attribute, so the compilation gate stays closed.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithoutWorkerAttributeAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
