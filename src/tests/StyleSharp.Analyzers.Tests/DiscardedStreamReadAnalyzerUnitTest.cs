// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyRead = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2446DiscardedStreamReadAnalyzer>;
using VerifyReadFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2446DiscardedStreamReadAnalyzer,
    StyleSharp.Analyzers.Sst2446DiscardedStreamReadCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2446 (a discarded stream read awaited through a configured awaiter or a local).</summary>
public class DiscardedStreamReadAnalyzerUnitTest
{
    /// <summary>Verifies parentheses and stored configured awaiters preserve discarded-read detection.</summary>
    /// <param name="body">The discarded read and expected diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("await ((stream.{|SST2446:ReadAsync|}(buffer, 0, buffer.Length))).ConfigureAwait(false);")]
    [Arguments("var read = ((stream.{|SST2446:ReadAsync|}(buffer, 0, buffer.Length))); await ((read)).ConfigureAwait(false);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ParenthesizedReadsAreReportedAsync(string body) => VerifyReportAsync($$"""
        using System.IO;
        using System.Threading.Tasks;
        class C
        {
            async Task M(Stream stream, byte[] buffer)
            {
                {{body}}
            }
        }
        """);

    /// <summary>Verifies reads reached by a simple method name retain their source location.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InheritedReadWithImplicitReceiverIsReportedAsync() => VerifyReportAsync("""
        using System.IO;
        using System.Threading.Tasks;
        class C : MemoryStream
        {
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken token) => base.ReadAsync(buffer, offset, count, token);
            async Task M(byte[] buffer)
            {
                await {|SST2446:ReadAsync|}(buffer, 0, buffer.Length, default).ConfigureAwait(false);
            }
        }
        """);

    /// <summary>Verifies unrelated await operands and locals without read initializers stay silent.</summary>
    /// <param name="body">The awaited expression and local declarations.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("await parameter;")]
    [Arguments("Task<int> local; local = parameter; await local;")]
    [Arguments("var local = parameter; await local;")]
    [Arguments("await (flag ? parameter : Task.FromResult(0));")]
    [Arguments("foreach (var local in new[] { parameter }) { await local; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AwaitWithoutReadInitializerIsSilentAsync(string body) => VerifyCleanAsync($$"""
        using System.Threading.Tasks;
        class C
        {
            async Task M(Task<int> parameter, bool flag)
            {
                {{body}}
            }
        }
        """);

    /// <summary>Verifies an unresolved read call does not produce an analyzer diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedReadIsSilentAsync() => new VerifyRead.Test
    {
        ReferenceAssemblies = AnalyzerFrameworks.Net90,
        CompilerDiagnostics = CompilerDiagnostics.None,
        TestCode = "using System.IO; using System.Threading.Tasks; class C { async Task M(Stream stream) { await stream.ReadAsync().ConfigureAwait(false); } }",
    }.RunAsync(CancellationToken.None);

    /// <summary>Verifies missing framework metadata prevents reporting even when the syntax matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingStreamMetadataIsSilentAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { async Task M() { await stream.ReadAsync(buffer).ConfigureAwait(false); } }");
        var compilation = CSharpCompilation.Create("MissingStream", [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2446DiscardedStreamReadAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies method-name extraction handles bindings, generic names and unsupported callees.</summary>
    /// <param name="source">The expression containing one invocation.</param>
    /// <param name="expected">The expected method name, if supported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("ReadAsync()", "ReadAsync")]
    [Arguments("ReadAsync<int>()", "ReadAsync")]
    [Arguments("stream.ReadAsync()", "ReadAsync")]
    [Arguments("stream?.ReadAsync()", "ReadAsync")]
    [Arguments("(read + other)()", null)]
    public async Task InvokedNameRecognizesSupportedShapesAsync(string source, string? expected)
    {
        var invocation = SyntaxFactory.ParseExpression(source).DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(Sst2446DiscardedStreamReadAnalyzer.GetInvokedName(invocation)).IsEqualTo(expected);
    }

    /// <summary>Verifies a same-named property does not advertise the read-exactly API.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReadExactlyPropertyStillSuggestsLoopAsync() => new VerifyRead.Test
    {
        ReferenceAssemblies = AnalyzerFrameworks.Net90,
        TestCode = """
            using System.Threading.Tasks;
            namespace System.IO
            {
                class Stream
                {
                    public int ReadExactlyAsync => 0;
                    public Task<int> ReadAsync() => Task.FromResult(0);
                }
            }
            class C
            {
                async Task M(System.IO.Stream stream)
                {
                    await stream.{|#0:ReadAsync|}().ConfigureAwait(false);
                }
            }
            """,
        ExpectedDiagnostics = { VerifyRead.Diagnostic().WithLocation(0).WithArguments("loop until the buffer is filled, or act on the returned count") },
    }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a configured-await discarded read is reported and rewritten to the read-exactly call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredAwaitFalseIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(Stream stream, Memory<byte> buffer)
                                  {
                                      await stream.{|SST2446:ReadAsync|}(buffer).ConfigureAwait(false);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(Stream stream, Memory<byte> buffer)
                                       {
                                           await stream.ReadExactlyAsync(buffer).ConfigureAwait(false);
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the configured-await(true) form is also reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredAwaitTrueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(Stream stream, Memory<byte> buffer)
                                  {
                                      await stream.{|SST2446:ReadAsync|}(buffer).ConfigureAwait(true);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(Stream stream, Memory<byte> buffer)
                                       {
                                           await stream.ReadExactlyAsync(buffer).ConfigureAwait(true);
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the byte-array overload is reported and fixed through the configured awaiter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByteArrayOverloadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(Stream stream, byte[] buffer)
                                  {
                                      await stream.{|SST2446:ReadAsync|}(buffer, 0, buffer.Length).ConfigureAwait(false);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(Stream stream, byte[] buffer)
                                       {
                                           await stream.ReadExactlyAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a read stored in a local and awaited as a statement is reported without a fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StoredThenAwaitedLocalIsFlaggedWithoutFixAsync() =>
        VerifyReportAsync(
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(Stream stream, Memory<byte> buffer)
                {
                    var read = stream.{|SST2446:ReadAsync|}(buffer);
                    await read;
                }
            }
            """);

    /// <summary>Verifies a bare await with no configured awaiter and no local is never reported here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BareAwaitIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(Stream stream, Memory<byte> buffer)
                {
                    await stream.ReadAsync(buffer);
                }
            }
            """);

    /// <summary>Verifies a read whose count is assigned is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AssignedCountIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<int> M(Stream stream, Memory<byte> buffer)
                {
                    int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                    return read;
                }
            }
            """);

    /// <summary>Verifies a read whose count is returned is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReturnedCountIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<int> M(Stream stream, Memory<byte> buffer)
                    => await stream.ReadAsync(buffer).ConfigureAwait(false);
            }
            """);

    /// <summary>Verifies a discarded write is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DiscardedWriteIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.IO;
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(Stream stream, ReadOnlyMemory<byte> buffer)
                {
                    await stream.WriteAsync(buffer).ConfigureAwait(false);
                }
            }
            """);

    /// <summary>Verifies a discarded read on a non-stream type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonStreamReadIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class Channel
            {
                public Task<int> ReadAsync(Memory<byte> buffer) => Task.FromResult(0);
            }

            public class C
            {
                public async Task M(Channel channel, Memory<byte> buffer)
                {
                    await channel.ReadAsync(buffer).ConfigureAwait(false);
                }
            }
            """);

    /// <summary>Verifies a local initialized with something other than a read is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalWithOtherInitializerIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    var work = Task.Delay(1);
                    await work;
                }
            }
            """);

    /// <summary>Verifies the read is reported without a fix where the read-exactly API is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardedReadIsReportedWithoutReadExactlyAsync()
    {
        const string Source = """
                              using System.IO;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(Stream stream, byte[] buffer)
                                  {
                                      await stream.{|SST2446:ReadAsync|}(buffer, 0, buffer.Length).ConfigureAwait(false);
                                  }
                              }
                              """;

        var test = new VerifyRead.Test { ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a report-and-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyReadFix.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a report-only verification (analyzer, no fix) against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new VerifyRead.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyCleanAsync(string source) => VerifyReportAsync(source);
}
