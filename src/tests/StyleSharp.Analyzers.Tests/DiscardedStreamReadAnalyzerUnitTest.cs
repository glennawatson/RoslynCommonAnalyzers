// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyRead = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2446DiscardedStreamReadAnalyzer>;
using VerifyReadFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2446DiscardedStreamReadAnalyzer,
    StyleSharp.Analyzers.Sst2446DiscardedStreamReadCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2446 (a discarded stream read awaited through a configured awaiter or a local).</summary>
public class DiscardedStreamReadAnalyzerUnitTest
{
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
    [Test]
    public async Task StoredThenAwaitedLocalIsFlaggedWithoutFixAsync()
        => await VerifyReportAsync(
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
    [Test]
    public async Task BareAwaitIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task AssignedCountIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task ReturnedCountIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task DiscardedWriteIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task NonStreamReadIsCleanAsync()
        => await VerifyCleanAsync(
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
    [Test]
    public async Task LocalWithOtherInitializerIsCleanAsync()
        => await VerifyCleanAsync(
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

        var test = new VerifyRead.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a report-and-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyReadFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a report-only verification (analyzer, no fix) against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new VerifyRead.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyReportAsync(source);
}
