// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1019UseAsSpanOverRangeIndexerAnalyzer,
    PerformanceSharp.Analyzers.Psh1019UseAsSpanOverRangeIndexerCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1019UseAsSpanOverRangeIndexerAnalyzer"/> (PSH1019 array range indexer).</summary>
public class UseAsSpanOverRangeIndexerAnalyzerUnitTest
{
    /// <summary>Verifies a range indexer consumed as a read-only span is flagged and sliced in place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RangeIndexerPassedAsSpanIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(byte[] data) => Use({|PSH1019:data[1..5]|});

                                  private static int Use(ReadOnlySpan<byte> bytes) => bytes.Length;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(byte[] data) => Use(data.AsSpan(1..5));

                                       private static int Use(ReadOnlySpan<byte> bytes) => bytes.Length;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a range indexer assigned to a read-only span local is flagged and sliced in place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RangeIndexerAssignedToSpanLocalIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(int[] data)
                                  {
                                      ReadOnlySpan<int> slice = {|PSH1019:data[..3]|};
                                      return slice.Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(int[] data)
                                       {
                                           ReadOnlySpan<int> slice = data.AsSpan(..3);
                                           return slice.Length;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a range indexer consumed as a read-only memory is sliced with AsMemory, not AsSpan.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RangeIndexerConsumedAsMemoryUsesAsMemoryAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public ReadOnlyMemory<byte> M(byte[] data) => {|PSH1019:data[2..]|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public ReadOnlyMemory<byte> M(byte[] data) => data.AsMemory(2..);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a slice the caller keeps as an array is not reported — the copy is the point.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RangeIndexerKeptAsArrayIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public byte[] M(byte[] data) => data[1..5];
            }
            """);

    /// <summary>Verifies a mutable span target is not reported, because a write would land on the original array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// This is the rule's central safety guard. <c>Span&lt;byte&gt; s = data[1..5]</c> hands the callee
    /// a private copy it may freely scribble on; <c>data.AsSpan(1..5)</c> hands it a window onto
    /// <c>data</c>. Those are not the same program, so the allocation is left alone.
    /// </remarks>
    [Test]
    public async Task MutableSpanTargetIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public void M(byte[] data)
                {
                    Span<byte> slice = data[1..5];
                    slice[0] = 42;
                }
            }
            """);

    /// <summary>Verifies a mutable memory target is not reported for the same reason.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutableMemoryTargetIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public Memory<byte> M(byte[] data) => data[1..5];
            }
            """);

    /// <summary>Verifies a range indexer on a string is not reported — there is no array to alias.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringRangeIndexerIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(string text) => text[1..5];
            }
            """);

    /// <summary>Verifies a range slice of a span is not reported — it never allocated in the first place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanRangeIndexerIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public ReadOnlySpan<byte> M(ReadOnlySpan<byte> data) => data[1..5];
            }
            """);

    /// <summary>Verifies a slice inside an ordinary lambda is still reported and still fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// This rule needs no expression-tree guard, because the compiler will not let a range indexer or a
    /// range expression into a tree in the first place (CS8790, CS8792). What it must not do is
    /// over-correct and go quiet inside every lambda — a delegate body is a perfectly good place for a
    /// span, and the slice there is as wasteful as anywhere else.
    /// </remarks>
    [Test]
    public async Task SliceInsideOrdinaryLambdaIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public Func<byte[], int> M() => data => Use({|PSH1019:data[1..5]|});

                                  private static int Use(ReadOnlySpan<byte> bytes) => bytes.Length;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public Func<byte[], int> M() => data => Use(data.AsSpan(1..5));

                                       private static int Use(ReadOnlySpan<byte> bytes) => bytes.Length;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>
    /// Verifies the rule registers nothing against netstandard2.0, where <c>MemoryExtensions</c> — and
    /// with it any range slice — does not exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The range indexer cannot even be written on that target (there is no <c>System.Range</c>), so
    /// the test compiles the shape the rule would otherwise chase — an array copied to be read — and
    /// asserts silence. The compilation-start gate is what produces that silence: it probes
    /// <c>MemoryExtensions</c> for a <c>System.Range</c>-taking slice and registers no syntax action
    /// when there is none, so nothing on this target can ever be reported.
    /// </remarks>
    [Test]
    public async Task NetStandard20IsSilentAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       using System;

                       public class C
                       {
                           public int M(byte[] data)
                           {
                               var copy = new byte[4];
                               Array.Copy(data, 1, copy, 0, 4);
                               return copy.Length;
                           }
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
