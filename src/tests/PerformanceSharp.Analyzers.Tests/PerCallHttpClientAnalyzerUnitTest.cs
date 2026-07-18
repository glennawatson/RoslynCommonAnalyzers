// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1418PerCallHttpClientAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1418PerCallHttpClientAnalyzer"/> (PSH1418 per-call HttpClient construction).</summary>
public class PerCallHttpClientAnalyzerUnitTest
{
    /// <summary>Minimal service-client surfaces declared under the real SDK namespaces, so the metadata-name probes resolve.</summary>
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

        namespace Azure.Messaging.ServiceBus
        {
            public class ServiceBusClient : System.IAsyncDisposable
            {
                public ServiceBusClient(string connectionString)
                {
                }

                public System.Threading.Tasks.ValueTask DisposeAsync() => default;
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

        namespace Azure.Security.KeyVault.Secrets
        {
            public class KeyVaultSecret
            {
            }

            public class SecretClient
            {
                public SecretClient(System.Uri vaultUri)
                {
                }

                public System.Threading.Tasks.Task<KeyVaultSecret> GetSecretAsync(string name)
                    => System.Threading.Tasks.Task.FromResult(new KeyVaultSecret());
            }
        }
        """;

    /// <summary>Verifies a using declaration over the construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> M(string url)
                {
                    using var client = {|PSH1418:new HttpClient()|};
                    return await client.GetStringAsync(url);
                }
            }
            """);

    /// <summary>Verifies a using statement over the construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStatementIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class C
            {
                public void M()
                {
                    using ({|PSH1418:new HttpClient()|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a using statement with a declarator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStatementWithDeclaratorIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> M(string url)
                {
                    using (var client = {|PSH1418:new HttpClient()|})
                    {
                        return await client.GetStringAsync(url);
                    }
                }
            }
            """);

    /// <summary>Verifies an inline receiver construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineReceiverIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> M(string url)
                    => await {|PSH1418:new HttpClient()|}.GetStringAsync(url);
            }
            """);

    /// <summary>Verifies a parenthesized inline receiver construction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedInlineReceiverIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> M(string url)
                    => await ({|PSH1418:new HttpClient()|}).GetStringAsync(url);
            }
            """);

    /// <summary>Verifies the target-typed using declaration form is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedUsingDeclarationIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> M(string url)
                {
                    using HttpClient client = {|PSH1418:new()|};
                    return await client.GetStringAsync(url);
                }
            }
            """);

    /// <summary>Verifies a service client used directly as a call receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlobServiceClientInlineReceiverIsFlaggedAsync()
        => await VerifyWithServiceClientsAsync(
            """
            using Azure.Storage.Blobs;
            using System.Threading.Tasks;

            public class C
            {
                public Task M(string connection)
                    => {|PSH1418:new BlobServiceClient(connection)|}.GetBlobContainerClient("data").CreateIfNotExistsAsync();
            }
            """);

    /// <summary>Verifies a using declaration over a disposable service client is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CosmosClientUsingDeclarationIsFlaggedAsync()
        => await VerifyWithServiceClientsAsync(
            """
            using Microsoft.Azure.Cosmos;

            public class C
            {
                public void M(string connection)
                {
                    using var client = {|PSH1418:new CosmosClient(connection)|};
                }
            }
            """);

    /// <summary>Verifies an await using declaration over an async-disposable service client is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ServiceBusClientAwaitUsingDeclarationIsFlaggedAsync()
        => await VerifyWithServiceClientsAsync(
            """
            using Azure.Messaging.ServiceBus;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(string connection)
                {
                    await using var client = {|PSH1418:new ServiceBusClient(connection)|};
                }
            }
            """);

    /// <summary>Verifies a secret client used directly as a call receiver is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecretClientInlineReceiverIsFlaggedAsync()
        => await VerifyWithServiceClientsAsync(
            """
            using System;
            using System.Threading.Tasks;
            using Azure.Security.KeyVault.Secrets;

            public class C
            {
                public Task<KeyVaultSecret> M(Uri vault, string name)
                    => {|PSH1418:new SecretClient(vault)|}.GetSecretAsync(name);
            }
            """);

    /// <summary>Verifies a static readonly field is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticFieldIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class C
            {
                private static readonly HttpClient Client = new HttpClient();

                public HttpClient Get() => Client;
            }
            """);

    /// <summary>Verifies an instance field is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceFieldIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class C
            {
                private readonly HttpClient _client = new HttpClient();

                public HttpClient Get() => _client;
            }
            """);

    /// <summary>Verifies a property initializer is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyInitializerIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class C
            {
                public HttpClient Client { get; } = new HttpClient();
            }
            """);

    /// <summary>Verifies a factory return is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactoryReturnIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class C
            {
                public HttpClient Create() => new HttpClient();

                public HttpClient CreateBlock()
                {
                    return new HttpClient();
                }
            }
            """);

    /// <summary>Verifies a plain local that is neither a using nor a receiver is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainLocalIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class C
            {
                public void M()
                {
                    var client = new HttpClient();
                    client.Dispose();
                }
            }
            """);

    /// <summary>Verifies a construction passed as an argument is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Net.Http;

            public class C
            {
                public void M() => Consume(new HttpClient());

                private static void Consume(HttpClient client)
                {
                }
            }
            """);

    /// <summary>Verifies another type's using construction is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using System.IO;

            public class C
            {
                public void M()
                {
                    using var stream = new MemoryStream();
                    stream.WriteByte(1);
                }
            }
            """);

    /// <summary>Verifies a service client cached in a static readonly field is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ServiceClientStaticFieldIsCleanAsync()
        => await VerifyWithServiceClientsAsync(
            """
            using Azure.Storage.Blobs;
            using System.Threading.Tasks;

            public class C
            {
                private static readonly BlobServiceClient Client = new BlobServiceClient("UseDevelopmentStorage=true");

                public Task M() => Client.GetBlobContainerClient("data").CreateIfNotExistsAsync();
            }
            """);

    /// <summary>Verifies a service client injected through the constructor is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedServiceClientIsCleanAsync()
        => await VerifyWithServiceClientsAsync(
            """
            using Microsoft.Azure.Cosmos;

            public class C
            {
                private readonly CosmosClient _client;

                public C(CosmosClient client) => _client = client;

                public CosmosClient Get() => _client;
            }
            """);

    /// <summary>Verifies a same-named type is never reported when the service client's package is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClientNameWithoutSdkIsCleanAsync()
        => await VerifyAsync(
            """
            namespace MyApp
            {
                public class CosmosClient : System.IDisposable
                {
                    public void Dispose()
                    {
                    }
                }

                public class C
                {
                    public void M()
                    {
                        using var client = new CosmosClient();
                    }
                }
            }
            """);

    /// <summary>Verifies a user type sharing a service client's simple name is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserTypeSharingClientNameIsCleanAsync()
        => await VerifyWithServiceClientsAsync(
            """
            namespace MyApp
            {
                public class BlobServiceClient
                {
                    public BlobServiceClient(string connectionString)
                    {
                    }

                    public System.Threading.Tasks.Task PingAsync() => System.Threading.Tasks.Task.CompletedTask;
                }

                public class C
                {
                    public System.Threading.Tasks.Task M(string connection) => new BlobServiceClient(connection).PingAsync();
                }
            }
            """);

    /// <summary>Verifies the assembly entry point is exempt.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EntryPointIsCleanAsync()
    {
        const string Source = """
                              using System.Net.Http;

                              using var client = new HttpClient();
                              System.Console.WriteLine(client);
                              """;

        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification with the service-client stub surfaces added to the compilation.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithServiceClientsAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.Sources.Add(ServiceClientStubsSource);

        await test.RunAsync(CancellationToken.None);
    }
}
