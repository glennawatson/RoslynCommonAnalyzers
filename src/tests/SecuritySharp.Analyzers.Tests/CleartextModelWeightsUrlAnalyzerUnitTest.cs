// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using AnalyzeWeightsUrl = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1606CleartextModelWeightsUrlAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1606 (a model-weights file must not be fetched over a cleartext http URL).</summary>
public class CleartextModelWeightsUrlAnalyzerUnitTest
{
    /// <summary>The cached primitive references for compilations without the framework HTTP client.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies a cleartext .onnx weights URL declared as a constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstOnnxWeightsUrlReportedAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/resnet50.onnx"|};
            }
            """);

    /// <summary>Verifies a cleartext .gguf weights URL held in a field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FieldGgufWeightsUrlReportedAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private static readonly string WeightsPath = {|SES1606:"http://cdn.example.com/llama-7b.gguf"|};
            }
            """);

    /// <summary>Verifies a cleartext .safetensors weights URL passed to a non-HttpClient loader is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SafetensorsPassedToLoaderReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StandaloneNewUriCkptReportedAsync() =>
        VerifyNet90Async(
            """
            using System;

            public class C
            {
                public Uri M() => new Uri({|SES1606:"http://weights.example.com/checkpoint.ckpt"|});
            }
            """);

    /// <summary>Verifies the .pt weights extension is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PtExtensionReportedAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/traced.pt"|};
            }
            """);

    /// <summary>Verifies the .pth weights extension is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PthExtensionReportedAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/net.pth"|};
            }
            """);

    /// <summary>Verifies a weights URL with a trailing query string is still reported (the path extension matches).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WeightsUrlWithQueryStringReportedAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"http://models.example.com/resnet50.onnx?token=abc"|};
            }
            """);

    /// <summary>Verifies the scheme and extension are matched case-insensitively.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UppercaseSchemeAndExtensionReportedAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = {|SES1606:"HTTP://Models.Example.Com/Resnet50.ONNX"|};
            }
            """);

    /// <summary>Verifies a name-colliding request method on a non-HttpClient type is still reported (the exclusion is HttpClient-specific).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonHttpClientRequestMethodReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task HttpsWeightsUrlIsCleanAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string ModelUrl = "https://models.example.com/resnet50.onnx";
            }
            """);

    /// <summary>Verifies a cleartext http URL with a non-weights extension is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonWeightsHttpUrlIsCleanAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string DataUrl = "http://data.example.com/config.json";
            }
            """);

    /// <summary>Verifies the broad .bin extension is deliberately not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BinExtensionIsCleanAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string BlobUrl = "http://cdn.example.com/pytorch_model.bin";
            }
            """);

    /// <summary>Verifies a loopback host weights URL is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoopbackHostIsCleanAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string LocalUrl = "http://localhost/model.onnx";
                private const string LoopbackUrl = "http://127.0.0.1:5000/model.gguf";
            }
            """);

    /// <summary>Verifies a weights extension that is not at the path end is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExtensionNotAtPathEndIsCleanAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string Url = "http://models.example.com/model.onnx/download";
            }
            """);

    /// <summary>Verifies an authority-only URL with no path is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task HostOnlyNoPathIsCleanAsync() =>
        VerifyNet90Async(
            """
            public class C
            {
                private const string Url = "http://models.example.com";
            }
            """);

    /// <summary>Verifies an inline weights URL passed straight to an HttpClient request is deferred to the transport rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InlineUrlToHttpClientRequestIsDeferredAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NewUriToHttpClientRequestIsDeferredAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NewUriToBaseAddressIsDeferredAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstWeightsUrlRequestedByHttpClientReportsDeclarationAsync() =>
        VerifyNet90Async(
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

    /// <summary>Verifies missing authorities, truncated paths, and authority queries cannot identify a weights download.</summary>
    /// <param name="url">The literal that must remain clean.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("")]
    [Arguments("http://")]
    [Arguments("http://models.example.com?file=/model.onnx")]
    [Arguments("http://models.example.com#file=/model.onnx")]
    [Arguments("http:///model.onnx")]
    [Arguments("http://models.example.com/")]
    [Arguments("http://models.example.com/a")]
    [Arguments("http://models.example.com/.p")]
    [Arguments("http://models.example.com/model.onnx.txt")]
    [Arguments("http://worker.localhost/model.onnx")]
    [Arguments("http://[::1]/model.onnx")]
    public Task IncompleteOrLocalWeightsUrlIsCleanAsync(string url) =>
        VerifyNet90Async($$"""
            class C { const string Url = "{{url}}"; }
            """);

    /// <summary>Verifies fragments do not hide a matching model extension.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FragmentAfterWeightsPathIsReportedAsync() =>
        VerifyNet90Async("""
            class C { const string Url = {|SES1606:"http://models.example.com/model.onnx#download"|}; }
            """);

    /// <summary>Verifies named request arguments and inherited base-address assignments remain owned by the transport rule.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedRequestsAndInheritedBaseAddressAreDeferredAsync() =>
        VerifyNet90Async("""
            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            class C : HttpClient
            {
                async Task M()
                {
                    BaseAddress = new Uri("http://models.example.com/model.onnx");
                    await this.GetAsync(cancellationToken: CancellationToken.None, requestUri: "http://models.example.com/model.onnx");
                    await this.GetAsync(requestUri: new Uri("http://models.example.com/model.onnx"));
                }
            }
            """);

    /// <summary>Verifies request-like calls and assignments outside the exact transport sink still report.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonTransportArgumentsAndAssignmentsAreReportedAsync() =>
        VerifyNet90Async("""
            using System;
            class OtherClient
            {
                public Uri BaseAddress { get; set; }
            }
            class Loader
            {
                public Uri BaseAddress;
                public Uri Address { get; set; }
                public void GetAsync(string address, string content = null) {}
                public void Load(string address) {}
                public void M(Loader loader, OtherClient other, dynamic unresolved)
                {
                    loader.GetAsync(address: {|SES1606:"http://models.example.com/model.onnx"|});
                    loader.GetAsync("https://models.example.com/", {|SES1606:"http://models.example.com/model.onnx"|});
                    loader.Load({|SES1606:"http://models.example.com/model.onnx"|});
                    unresolved.GetAsync({|SES1606:"http://models.example.com/model.onnx"|});
                    loader.Address = new Uri({|SES1606:"http://models.example.com/model.onnx"|});
                    loader.BaseAddress = new Uri({|SES1606:"http://models.example.com/model.onnx"|});
                    other.BaseAddress = new Uri({|SES1606:"http://models.example.com/model.onnx"|});
                }
            }
            """);

    /// <summary>Verifies constructor initializers and target-typed construction are not transport request arguments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructorArgumentShapesAreReportedAsync() =>
        VerifyNet90Async("""
            using System;
            class C
            {
                public C() : this({|SES1606:"http://models.example.com/model.onnx"|}) {}
                public C(string path) {}
                public Uri Create() => new({|SES1606:"http://models.example.com/model.onnx"|});
            }
            """);

    /// <summary>Verifies lookalike client types do not acquire the framework transport exclusion.</summary>
    /// <param name="declaration">The type whose request method resembles the framework client.</param>
    /// <param name="typeName">The fully qualified type used by the caller.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class HttpClient { public void GetAsync(string requestUri) {} }", "HttpClient")]
    [Arguments("namespace System.Net.Http { class HttpClient<T> { public void GetAsync(string requestUri) {} } }", "System.Net.Http.HttpClient<int>")]
    [Arguments("class Outer { public class HttpClient { public void GetAsync(string requestUri) {} } }", "Outer.HttpClient")]
    [Arguments("namespace Other.Net.Http { class HttpClient { public void GetAsync(string requestUri) {} } }", "Other.Net.Http.HttpClient")]
    [Arguments("namespace Other.System.Net.Http { class HttpClient { public void GetAsync(string requestUri) {} } }", "Other.System.Net.Http.HttpClient")]
    [Arguments("namespace System.Other.Http { class HttpClient { public void GetAsync(string requestUri) {} } }", "System.Other.Http.HttpClient")]
    [Arguments("namespace System.Net.Other { class HttpClient { public void GetAsync(string requestUri) {} } }", "System.Net.Other.HttpClient")]
    public async Task LookalikeClientStillReportsWithoutFrameworkHttpClientAsync(string declaration, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            {{declaration}}
            class C { void M({{typeName}} client) { client.GetAsync("http://models.example.com/model.onnx"); } }
            """);
        var compilation = CSharpCompilation.Create("LookalikeClient", [tree], CoreReferences, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Ses1606CleartextModelWeightsUrlAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SES1606");
        await Assert.That(diagnostics[0].GetMessage()).Contains("models.example.com");
    }

    /// <summary>Verifies wrapped URLs in constructor arguments or invalid assignment targets are not transport sinks.</summary>
    /// <param name="body">The constructor use containing the cleartext URL.</param>
    /// <param name="invalidAssignment">Whether the compiler rejects the assignment target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new Wrapper(new C(\"http://models.example.com/model.onnx\"));", false)]
    [Arguments("new C(\"http://models.example.com/model.onnx\") = null;", true)]
    public async Task NonRequestConstructionStillReportsAsync(string body, bool invalidAssignment)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            class C
            {
                public C(string path) { }
                void M() { {{body}} }
            }
            class Wrapper { public Wrapper(C value) { } }
            """);
        var compilation = CSharpCompilation.Create("NonRequestConstruction", [tree], CoreReferences, new(OutputKind.DynamicallyLinkedLibrary));
        await Assert.That(compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS0131")).IsEqualTo(invalidAssignment);
        var diagnostics = await compilation.WithAnalyzers([new Ses1606CleartextModelWeightsUrlAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SES1606");
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where HttpClient exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeWeightsUrl.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
