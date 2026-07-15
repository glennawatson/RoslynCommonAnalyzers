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
}
