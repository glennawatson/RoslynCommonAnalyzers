// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyMemory = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1314UseMemoryBasedStreamOverloadsAnalyzer,
    PerformanceSharp.Analyzers.Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1314 (read and write streams through the memory-based overloads) and its code fix.</summary>
public class UseMemoryBasedStreamOverloadsAnalyzerUnitTest
{
    /// <summary>Verifies an awaited array-based ReadAsync is reported and rewritten through AsMemory.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayReadAsyncIsRewrittenAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Stream stream, byte[] buffer)
                                      => await {|PSH1314:stream.ReadAsync(buffer, 0, buffer.Length)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M(Stream stream, byte[] buffer)
                                           => await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an awaited array-based WriteAsync is reported and rewritten through AsMemory.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayWriteAsyncIsRewrittenAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task M(Stream stream, byte[] buffer)
                                      => await {|PSH1314:stream.WriteAsync(buffer, 0, buffer.Length)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task M(Stream stream, byte[] buffer)
                                           => await stream.WriteAsync(buffer.AsMemory(0, buffer.Length));
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a trailing cancellation token is carried over to the memory overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CancellationTokenIsCarriedOverAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Stream stream, byte[] buffer, CancellationToken token)
                                      => await {|PSH1314:stream.ReadAsync(buffer, 0, buffer.Length, token)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;
                                   using System.Threading;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M(Stream stream, byte[] buffer, CancellationToken token)
                                           => await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a call awaited through ConfigureAwait is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredAwaitIsRewrittenAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Stream stream, byte[] buffer)
                                      => await {|PSH1314:stream.ReadAsync(buffer, 0, buffer.Length)|}.ConfigureAwait(false);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.IO;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<int> M(Stream stream, byte[] buffer)
                                           => await stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a call whose task is stored rather than awaited is not reported, because the task type would change.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StoredTaskIsNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Stream stream, byte[] buffer)
                                  {
                                      Task<int> pending = stream.ReadAsync(buffer, 0, buffer.Length);
                                      return await pending;
                                  }
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a call that already uses the memory overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemoryOverloadIsNotReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.IO;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<int> M(Stream stream, byte[] buffer)
                                      => await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyMemory.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
