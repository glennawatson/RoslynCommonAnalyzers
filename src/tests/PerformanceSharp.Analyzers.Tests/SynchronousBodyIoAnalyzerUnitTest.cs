// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    [Test]
    public async Task RequestBodyReadByteReportedAsync()
        => await VerifyAnalyzerAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public int Handle(HttpContext ctx) => {|PSH1506:ctx.Request.Body.ReadByte()|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a direct synchronous write of the response body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResponseBodyWriteReportedAsync()
        => await VerifyAnalyzerAsync(
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
    [Test]
    public async Task ResponseBodyFlushReportedAsync()
        => await VerifyAnalyzerAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handler
            {
                public void Handle(HttpContext ctx) => {|PSH1506:ctx.Response.Body.Flush()|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a synchronous read through a reader wrapping the request body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StreamReaderReadToEndReportedAsync()
        => await VerifyAnalyzerAsync(
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
    [Test]
    public async Task LocalBodyStreamReadReportedAsync()
        => await VerifyAnalyzerAsync(
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
    [Test]
    public async Task LocalStreamReaderReadToEndReportedAsync()
        => await VerifyAnalyzerAsync(
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
    [Test]
    public async Task UnrelatedStreamIsCleanAsync()
        => await VerifyAnalyzerAsync(
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
    [Test]
    public async Task AsyncBodyReadIsCleanAsync()
        => await VerifyAnalyzerAsync(
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
    [Test]
    public async Task SilentWhenHttpRequestAbsentAsync()
        => await VerifyAnalyzerAsync(
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

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new AnalyzeBodyIo.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        var test = new VerifyBodyIoFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
