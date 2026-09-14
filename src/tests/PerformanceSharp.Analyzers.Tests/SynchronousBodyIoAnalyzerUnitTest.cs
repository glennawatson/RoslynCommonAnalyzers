// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using AnalyzeBodyIo = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1506SynchronousBodyIoAnalyzer>;
using VerifyBodyIoFix = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1506SynchronousBodyIoAnalyzer,
    PerformanceSharp.Analyzers.Psh1506SynchronousBodyIoCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1506 (synchronous read/write of the HTTP request or response body) and its conditional code fix.</summary>
public class SynchronousBodyIoAnalyzerUnitTest
{
    /// <summary>The inline ASP.NET Core stubs; the referenced framework does not carry these types.</summary>
    private const string AspNetStubs = """

        namespace Microsoft.AspNetCore.Http
        {
            public abstract class HttpRequest
            {
                public abstract System.IO.Stream Body { get; set; }
            }

            public abstract class HttpResponse
            {
                public abstract System.IO.Stream Body { get; set; }
            }

            public abstract class HttpContext
            {
                public abstract HttpRequest Request { get; }

                public abstract HttpResponse Response { get; }
            }
        }
        """;

    /// <summary>Verifies a direct synchronous read of the request body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RequestBodyReadByteReportedAsync() =>
        VerifyAnalyzerAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public int Handle(HttpContext ctx) => {|PSH1506:ctx.Request.Body.ReadByte()|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a direct synchronous write of the response body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ResponseBodyWriteReportedAsync() =>
        VerifyAnalyzerAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public void Handle(HttpContext ctx, byte[] buffer)
                    => {|PSH1506:ctx.Response.Body.Write(buffer, 0, buffer.Length)|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a synchronous flush of the response body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ResponseBodyFlushReportedAsync() =>
        VerifyAnalyzerAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public void Handle(HttpContext ctx) => {|PSH1506:ctx.Response.Body.Flush()|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a synchronous read through a reader wrapping the request body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StreamReaderReadToEndReportedAsync() =>
        VerifyAnalyzerAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public string Handle(HttpContext ctx) => {|PSH1506:new StreamReader(ctx.Request.Body).ReadToEnd()|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a synchronous read on a local initialised from the request body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalBodyStreamReadReportedAsync() =>
        VerifyAnalyzerAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public int Handle(HttpContext ctx, byte[] buffer)
                {
                    Stream body = ctx.Request.Body;
                    return {|PSH1506:body.Read(buffer, 0, buffer.Length)|};
                }
            }
            """ + AspNetStubs);

    /// <summary>Verifies a synchronous read on a local reader initialised over the request body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalStreamReaderReadToEndReportedAsync() =>
        VerifyAnalyzerAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public string Handle(HttpContext ctx)
                {
                    var reader = new StreamReader(ctx.Request.Body);
                    return {|PSH1506:reader.ReadToEnd()|};
                }
            }
            """ + AspNetStubs);

    /// <summary>Verifies a synchronous read on an unrelated stream is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedStreamIsCleanAsync() =>
        VerifyAnalyzerAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public int Handle(HttpContext ctx)
                {
                    var memory = new MemoryStream();
                    return memory.ReadByte();
                }
            }
            """ + AspNetStubs);

    /// <summary>Verifies an already asynchronous body read is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncBodyReadIsCleanAsync() =>
        VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public async Task<int> HandleAsync(HttpContext ctx, byte[] buffer)
                    => await ctx.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            }
            """ + AspNetStubs);

    /// <summary>Verifies the rule stays silent when the ASP.NET Core request type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SilentWhenHttpRequestAbsentAsync() =>
        VerifyAnalyzerAsync(
            """
            public class Handler
            {
                public int Handle(MyWeb.HttpContext ctx) => ctx.Request.Body.ReadByte();
            }

            namespace MyWeb
            {
                public abstract class HttpRequest
                {
                    public abstract System.IO.Stream Body { get; set; }
                }

                public abstract class HttpContext
                {
                    public abstract HttpRequest Request { get; }
                }
            }
            """);

    /// <summary>Verifies a reader read over the body in an async method is rewritten to its awaited async overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StreamReaderReadToEndInAsyncMethodIsRewrittenAsync()
    {
        const string Source = """
                              using System.IO;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Http;

                              public class Handler
                              {
                                  public async Task<string> HandleAsync(HttpContext ctx)
                                      => {|PSH1506:new StreamReader(ctx.Request.Body).ReadToEnd()|};
                              }
                              """ + AspNetStubs;
        const string FixedSource = """
                                   using System.IO;
                                   using System.Threading.Tasks;
                                   using Microsoft.AspNetCore.Http;

                                   public class Handler
                                   {
                                       public async Task<string> HandleAsync(HttpContext ctx)
                                           => await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                                   }
                                   """ + AspNetStubs;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a response flush in an async method is rewritten to an awaited flush.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResponseFlushInAsyncMethodIsRewrittenAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Http;

                              public class Handler
                              {
                                  public async Task HandleAsync(HttpContext ctx)
                                  {
                                      {|PSH1506:ctx.Response.Body.Flush()|};
                                  }
                              }
                              """ + AspNetStubs;
        const string FixedSource = """
                                   using System.Threading.Tasks;
                                   using Microsoft.AspNetCore.Http;

                                   public class Handler
                                   {
                                       public async Task HandleAsync(HttpContext ctx)
                                       {
                                           await ctx.Response.Body.FlushAsync();
                                       }
                                   }
                                   """ + AspNetStubs;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a response write in an async method carries its arguments to the awaited async overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResponseWriteInAsyncMethodIsRewrittenAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Http;

                              public class Handler
                              {
                                  public async Task HandleAsync(HttpContext ctx, byte[] buffer)
                                  {
                                      {|PSH1506:ctx.Response.Body.Write(buffer, 0, buffer.Length)|};
                                  }
                              }
                              """ + AspNetStubs;
        const string FixedSource = """
                                   using System.Threading.Tasks;
                                   using Microsoft.AspNetCore.Http;

                                   public class Handler
                                   {
                                       public async Task HandleAsync(HttpContext ctx, byte[] buffer)
                                       {
                                           await ctx.Response.Body.WriteAsync(buffer, 0, buffer.Length);
                                       }
                                   }
                                   """ + AspNetStubs;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the awaited replacement is parenthesized when the surrounding expression binds tighter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitIsParenthesizedWhenChainedAsync()
    {
        const string Source = """
                              using System.IO;
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Http;

                              public class Handler
                              {
                                  public async Task<int> HandleAsync(HttpContext ctx)
                                      => {|PSH1506:new StreamReader(ctx.Request.Body).ReadToEnd()|}.Length;
                              }
                              """ + AspNetStubs;
        const string FixedSource = """
                                   using System.IO;
                                   using System.Threading.Tasks;
                                   using Microsoft.AspNetCore.Http;

                                   public class Handler
                                   {
                                       public async Task<int> HandleAsync(HttpContext ctx)
                                           => (await new StreamReader(ctx.Request.Body).ReadToEndAsync()).Length;
                                   }
                                   """ + AspNetStubs;
        await VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a body read in a synchronous method is reported but offered no fix — there is no legal place for the await.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyncMethodBodyReadReportedWithoutFixAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Http;

                              public class Handler
                              {
                                  public int Handle(HttpContext ctx, byte[] buffer)
                                      => {|PSH1506:ctx.Request.Body.Read(buffer, 0, buffer.Length)|};
                              }
                              """ + AspNetStubs;
        await VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a body read whose async form has a different signature is reported but offered no fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncMethodReadByteReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Http;

                              public class Handler
                              {
                                  public async Task HandleAsync(HttpContext ctx)
                                  {
                                      {|PSH1506:ctx.Request.Body.ReadByte()|};
                                  }
                              }
                              """ + AspNetStubs;
        await VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a body read reached through a conditional access is reported but offered no fix — rebinding the detached call would orphan its member binding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessBodyReadReportsWithoutOfferingAFixAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;
                              using Microsoft.AspNetCore.Http;

                              public class Handler
                              {
                                  public async Task HandleAsync(HttpRequest req, byte[] buffer)
                                  {
                                      req?{|PSH1506:.Body.Read(buffer, 0, buffer.Length)|};
                                      await Task.Yield();
                                  }
                              }
                              """ + AspNetStubs;
        await VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies the remaining synchronous stream and reader methods are reported.</summary>
    /// <param name="expression">The synchronous HTTP body operation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("((ctx.Request.Body)).WriteByte(1)")]
    [Arguments("ctx.Response.Body.CopyTo(System.IO.Stream.Null)")]
    [Arguments("new System.IO.StreamReader((ctx.Request.Body)).ReadLine()")]
    [Arguments("new global::System.IO.StreamReader(ctx.Request.Body).ReadBlock(new char[1], 0, 1)")]
    [Arguments("new System.IO.StreamWriter(ctx.Response.Body).Flush()")]
    public Task SynchronousCounterpartsAreReportedAsync(string expression) =>
        VerifyAnalyzerAsync($$"""
            class Handler
            {
                void M(Microsoft.AspNetCore.Http.HttpContext ctx)
                {
                    {|PSH1506:{{expression}}|};
                }
            }
            """ + AspNetStubs);

    /// <summary>Verifies local initializer parentheses are peeled before identifying body streams.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ParenthesizedLocalInitializersAreReportedAsync() =>
        VerifyAnalyzerAsync("""
            class Handler
            {
                void M(Microsoft.AspNetCore.Http.HttpContext ctx)
                {
                    var body = ((ctx.Request.Body));
                    {|PSH1506:(body).Flush()|};
                    var writer = (new System.IO.StreamWriter(ctx.Response.Body));
                    {|PSH1506:writer.Write('x')|};
                }
            }
            """ + AspNetStubs);

    /// <summary>Verifies unrelated receivers and untracked aliases do not report body I/O.</summary>
    /// <param name="statement">The unrelated or untracked operation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("input.ReadByte();")]
    [Arguments("Other.ReadByte();")]
    [Arguments("Body.ReadByte();")]
    [Arguments("var stream = input; stream.Flush();")]
    [Arguments("System.IO.Stream stream; stream = ctx.Request.Body; stream.Flush();")]
    [Arguments("var first = ctx.Request.Body; var second = first; second.Flush();")]
    [Arguments("new System.IO.StreamReader(input).ReadToEnd();")]
    [Arguments("new System.IO.StreamReader(Other).ReadToEnd();")]
    [Arguments("new System.IO.StreamReader(Body).ReadToEnd();")]
    [Arguments("new System.IO.StreamReader(System.IO.Stream.Null).ReadToEnd();")]
    [Arguments("new System.IO.StreamReader(new System.IO.MemoryStream()).ReadToEnd();")]
    [Arguments("new System.IO.StreamReader(\"file.txt\").ReadToEnd();")]
    [Arguments("new System.IO.StreamReader((System.IO.Stream)input).ReadToEnd();")]
    [Arguments("new System.IO.MemoryStream().ReadByte();")]
    [Arguments("var reader = new System.IO.StreamReader(input); reader.ReadToEnd();")]
    [Arguments("ctx.Request.Body.Close();")]
    [Arguments("Read();")]
    public Task UnrelatedReceiversAreCleanAsync(string statement) =>
        VerifyAnalyzerAsync($$"""
            class Handler
            {
                System.IO.Stream Other => System.IO.Stream.Null;
                System.IO.Stream Body => System.IO.Stream.Null;
                void Read() { }
                void M(Microsoft.AspNetCore.Http.HttpContext ctx, System.IO.Stream input)
                {
                    {{statement}}
                }
            }
            """ + AspNetStubs);

    /// <summary>Verifies an inherited HTTP body property and an inherited async method are recognized.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedBodyAndAsyncMethodAreReportedAsync() =>
        VerifyAnalyzerAsync("""
            namespace Microsoft.AspNetCore.Http { public class HttpRequest { } }
            class Request : Microsoft.AspNetCore.Http.HttpRequest { public BodyStream Body => new BodyStream(); }
            class AsyncStream { public void FlushAsync() { } }
            class BodyStream : AsyncStream { public void Flush() { } }
            class Handler { void M(Request request) { {|PSH1506:request.Body.Flush()|}; } }
            """);

    /// <summary>Verifies a missing async method, including a same-named property, prevents a diagnostic.</summary>
    /// <param name="asyncMember">The unavailable or nonmethod async counterpart.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("")]
    [Arguments("public int FlushAsync => 0;")]
    public Task MissingAsyncCounterpartIsCleanAsync(string asyncMember) =>
        VerifyAnalyzerAsync($$"""
            namespace Microsoft.AspNetCore.Http
            {
                public class HttpRequest { public BodyStream Body => new BodyStream(); }
            }
            public class BodyStream { public void Flush() { } {{asyncMember}} }
            class Handler { void M(Microsoft.AspNetCore.Http.HttpRequest request) { request.Body.Flush(); } }
            """);

    /// <summary>Verifies wrapper argument scanning skips nonbody arguments and recognizes a bare Body property.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WrapperArgumentsAndAliasQualifiedNamesAreRecognizedAsync() =>
        VerifyAnalyzerAsync("""
            namespace Microsoft.AspNetCore.Http
            {
                public class HttpRequest { public System.IO.Stream Body => System.IO.Stream.Null; }
            }
            class StreamReader
            {
                public StreamReader(int count, System.IO.Stream stream) { }
                public void ReadLine() { }
                public void ReadLineAsync() { }
            }
            class Request : Microsoft.AspNetCore.Http.HttpRequest
            {
                void M() { {|PSH1506:new global::StreamReader(1, Body).ReadLine()|}; }
            }
            """);

    /// <summary>Verifies wrapper construction without body arguments and unbound type shapes are ignored.</summary>
    /// <param name="expression">The receiver construction.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("new StreamReader { }")]
    [Arguments("new StreamReader()")]
    [Arguments("new StreamReader(new())")]
    [Arguments("new StreamReader(input)")]
    [Arguments("new StreamReader(other.Body)")]
    [Arguments("new StreamReader(request?.Body)")]
    [Arguments("new int()")]
    public Task WrapperWithoutBodyArgumentIsCleanAsync(string expression) =>
        new AnalyzeBodyIo.Test
        {
            TestCode = $$"""
                namespace Microsoft.AspNetCore.Http
                {
                    public class HttpRequest { public System.IO.Stream Body => System.IO.Stream.Null; }
                }
                class StreamReader
                {
                    public StreamReader() { }
                    public StreamReader(System.IO.MemoryStream stream) { }
                    public void ReadLine() { }
                    public void ReadLineAsync() { }
                }
                class Other { public System.IO.MemoryStream Body => new(); }
                class C
                {
                    void M(Microsoft.AspNetCore.Http.HttpRequest request, Other other, System.IO.MemoryStream input)
                    {
                        {{expression}}.ReadLine();
                    }
                }
                """,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies failed invocation binding is ignored after a receiver is recognized as the body.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedBodyInvocationIsCleanAsync() =>
        new AnalyzeBodyIo.Test
        {
            TestCode = $$"""
                class C { void M(Microsoft.AspNetCore.Http.HttpRequest request) { request.Body.Read("invalid"); } }
                {{AspNetStubs}}
                """,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new AnalyzeBodyIo.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        var test = new VerifyBodyIoFix.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }
}
