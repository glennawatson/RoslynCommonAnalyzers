// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyMemory = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1314UseMemoryBasedStreamOverloadsAnalyzer,
    PerformanceSharp.Analyzers.Psh1314UseMemoryBasedStreamOverloadsCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1314 (read and write streams through the memory-based overloads) and its code fix.</summary>
public class UseMemoryBasedStreamOverloadsAnalyzerUnitTest
{
    /// <summary>Verifies legacy frameworks do not receive a replacement API they cannot call.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FrameworkWithoutMemoryOverloadsIsCleanAsync()
    {
        var test = new VerifyMemory.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = """
                class C
                {
                    async System.Threading.Tasks.Task<int> M(System.IO.Stream stream, byte[] buffer)
                        => await stream.ReadAsync(buffer, 0, buffer.Length);
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies same-named calls on unrelated objects and non-awaited configured tasks remain clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonStreamAndStoredConfiguredCallsAreCleanAsync() =>
        VerifyMemory.VerifyAnalyzerAsync("""
            using System.Threading.Tasks;
            class Other { public Task<int> ReadAsync(byte[] buffer, int offset, int count) => Task.FromResult(0); }
            class C
            {
                async Task<int> M(Other other, System.IO.Stream stream, byte[] buffer)
                {
                    var configured = stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    return await other.ReadAsync(buffer, 0, buffer.Length) + await configured;
                }
            }
            """);

    /// <summary>Verifies derived stream overloads must have the precise byte-array, offset, count, and token signature.</summary>
    /// <param name="parameters">The non-matching declaration parameters.</param>
    /// <param name="arguments">The arguments selecting that overload.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("object buffer, int offset, int count", "new object(), 0, 1")]
    [Arguments("char[] buffer, int offset, int count", "new char[1], 0, 1")]
    [Arguments("byte[] buffer, long offset, int count", "buffer, 0L, 1")]
    [Arguments("byte[] buffer, int offset, long count", "buffer, 0, 1L")]
    [Arguments("byte[] buffer, int offset, int count, int token", "buffer, 0, 1, 0")]
    [Arguments("params int[] values", "0, 1, 2")]
    public async Task DifferentArraySignaturesAreCleanAsync(string parameters, string arguments)
    {
        var test = new VerifyMemory.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                using System.Threading.Tasks;
                class Custom : System.IO.MemoryStream
                {
                    public Task<int> ReadAsync({{parameters}}) => Task.FromResult(0);
                }
                class C
                {
                    async Task<int> M(Custom stream, byte[] buffer) => await stream.ReadAsync({{arguments}});
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies memory candidates with required trailing parameters cannot replace a one-argument call.</summary>
    /// <param name="members">The candidate memory overloads or same-named non-method.</param>
    /// <param name="found">Whether a callable memory overload exists.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("public void ReadAsync(System.Memory<byte> buffer, int required) { }", false)]
    [Arguments("public void ReadAsync(System.Memory<byte> buffer, int optional = 0) { }", true)]
    [Arguments("public int ReadAsync { get; }", false)]
    [Arguments("public void ReadAsync() { }", false)]
    public async Task MemoryOverloadRequiresOptionalTrailingParametersAsync(string members, bool found)
    {
        var compilation = CSharpCompilation.Create(
            nameof(MemoryOverloadRequiresOptionalTrailingParametersAsync),
            [CSharpSyntaxTree.ParseText($"class C {{ {members} }}")],
            RuntimeMetadataReferences.Platform);
        var containingType = compilation.GetTypeByMetadataName("C")!;
        var memory = compilation.GetTypeByMetadataName("System.Memory`1")!
            .Construct(compilation.GetSpecialType(Microsoft.CodeAnalysis.SpecialType.System_Byte));
        var overload = Psh1314UseMemoryBasedStreamOverloadsAnalyzer.TryFindMemoryOverload(containingType, "ReadAsync", memory);
        await Assert.That(overload is not null).IsEqualTo(found);
    }

    /// <summary>Verifies target streams with only a memory read overload do not suggest a missing memory write overload.</summary>
    /// <param name="optional">Whether the memory read's trailing parameter is optional.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task MissingCallableMemoryOverloadIsCleanAsync(bool optional)
    {
        var test = new VerifyMemory.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""
                using System.Threading.Tasks;
                namespace System.IO
                {
                    public class Stream
                    {
                        public Task<int> ReadAsync(System.Memory<byte> buffer, int extra{{(optional ? " = 0" : string.Empty)}}) => Task.FromResult(0);
                        public Task WriteAsync(byte[] buffer, int offset, int count) => Task.CompletedTask;
                    }
                }
                class C
                {
                    async Task M(System.IO.Stream stream, byte[] buffer) => await stream.WriteAsync(buffer, 0, buffer.Length);
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies each missing framework type prevents memory suggestions on partially loaded source.</summary>
    /// <param name="types">The available framework declarations in dependency order.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("")]
    [Arguments("namespace System.IO { class Stream { } }")]
    [Arguments("namespace System.IO { class Stream { } } namespace System { struct Memory<T> { } }")]
    [Arguments("namespace System.IO { class Stream { } } namespace System { struct Memory<T> { } struct ReadOnlyMemory<T> { } }")]
    public async Task MissingFrameworkTypesAreCleanAsync(string types)
    {
        var compilation = CSharpCompilation.Create(
            nameof(MissingFrameworkTypesAreCleanAsync),
            [CSharpSyntaxTree.ParseText(types + "class C { async void M(dynamic stream) { await stream.ReadAsync(null, 0, 1); } }")]);
        var diagnostics = await compilation.WithAnalyzers([new Psh1314UseMemoryBasedStreamOverloadsAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

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
        var test = new VerifyMemory.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }
}
