// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeWeightsUrl = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1606CleartextModelWeightsUrlAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1606 (a model-weights file must not be fetched over a cleartext http URL).</summary>
public class CleartextModelWeightsUrlAnalyzerUnitTest
{
    /// <summary>Verifies a cleartext .onnx weights URL declared as a constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstOnnxWeightsUrlReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/resnet50.onnx"|};
            }
            """);

    /// <summary>Verifies a cleartext .gguf weights URL held in a field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldGgufWeightsUrlReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private static readonly string WeightsPath = {|SES1606:"http://cdn.example.com/llama-7b.gguf"|};
            }
            """);

    /// <summary>Verifies a cleartext .safetensors weights URL passed to a non-HttpClient loader is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SafetensorsPassedToLoaderReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public void M() => LoadModel({|SES1606:"http://ml.example.com/model.safetensors"|});

                private static void LoadModel(string path)
                {
                }
            }
            """);

    /// <summary>Verifies a cleartext .ckpt weights URL inside a standalone new Uri(...) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StandaloneNewUriCkptReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public Uri M() => new Uri({|SES1606:"http://weights.example.com/checkpoint.ckpt"|});
            }
            """);

    /// <summary>Verifies the .pt weights extension is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PtExtensionReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/traced.pt"|};
            }
            """);

    /// <summary>Verifies the .pth weights extension is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PthExtensionReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/net.pth"|};
            }
            """);

    /// <summary>Verifies a weights URL with a trailing query string is still reported (the path extension matches).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WeightsUrlWithQueryStringReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/resnet50.onnx?token=abc"|};
            }
            """);

    /// <summary>Verifies the scheme and extension are matched case-insensitively.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UppercaseSchemeAndExtensionReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"HTTP://Models.Example.Com/Resnet50.ONNX"|};
            }
            """);

    /// <summary>Verifies a name-colliding request method on a non-HttpClient type is still reported (the exclusion is HttpClient-specific).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonHttpClientRequestMethodReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public sealed class MyClient
            {
                public Task<byte[]> GetByteArrayAsync(string url) => Task.FromResult(new byte[0]);
            }

            public class C
            {
                public async Task M(MyClient client)
                {
                    await client.GetByteArrayAsync({|SES1606:"http://models.example.com/traced.pt"|});
                }
            }
            """);

    /// <summary>Verifies an https weights URL is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HttpsWeightsUrlIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = "https://models.example.com/resnet50.onnx";
            }
            """);

    /// <summary>Verifies a cleartext http URL with a non-weights extension is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonWeightsHttpUrlIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string DataUrl = "http://data.example.com/config.json";
            }
            """);

    /// <summary>Verifies the broad .bin extension is deliberately not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BinExtensionIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string BlobUrl = "http://cdn.example.com/pytorch_model.bin";
            }
            """);

    /// <summary>Verifies a loopback host weights URL is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoopbackHostIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string LocalUrl = "http://localhost/model.onnx";
                private const string LoopbackUrl = "http://127.0.0.1:5000/model.gguf";
            }
            """);

    /// <summary>Verifies a weights extension that is not at the path end is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionNotAtPathEndIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string Url = "http://models.example.com/model.onnx/download";
            }
            """);

    /// <summary>Verifies an authority-only URL with no path is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HostOnlyNoPathIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                private const string Url = "http://models.example.com";
            }
            """);

    /// <summary>Verifies an inline weights URL passed straight to an HttpClient request is deferred to the transport rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineUrlToHttpClientRequestIsDeferredAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetByteArrayAsync("http://models.example.com/traced.pt");
                }
            }
            """);

    /// <summary>Verifies a new Uri(...) weights URL passed to an HttpClient request is deferred to the transport rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewUriToHttpClientRequestIsDeferredAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(HttpClient client)
                {
                    await client.GetByteArrayAsync(new Uri("http://models.example.com/model.onnx"));
                }
            }
            """);

    /// <summary>Verifies a new Uri(...) weights URL assigned to HttpClient.BaseAddress is deferred to the transport rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewUriToBaseAddressIsDeferredAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Net.Http;

            public class C
            {
                public void M(HttpClient client)
                {
                    client.BaseAddress = new Uri("http://models.example.com/model.onnx");
                }
            }
            """);

    /// <summary>Verifies a weights URL held in a constant and then requested by HttpClient reports only the constant declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstWeightsUrlRequestedByHttpClientReportsDeclarationAsync()
        => await VerifyNet90Async(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/model.onnx"|};

                public async Task M(HttpClient client)
                {
                    await client.GetByteArrayAsync(ModelUrl);
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where HttpClient exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeWeightsUrl.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
