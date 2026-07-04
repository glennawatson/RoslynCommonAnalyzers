// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1013Utf8SpanPropertyAnalyzer,
    PerformanceSharp.Analyzers.Psh1013Utf8SpanPropertyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1013Utf8SpanPropertyAnalyzer"/> (PSH1013 UTF-8 span properties).</summary>
public class Utf8SpanPropertyAnalyzerUnitTest
{
    /// <summary>Verifies a u8 ToArray field with span-only reads is flagged and becomes a property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToArrayFieldIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private static readonly byte[] {|PSH1013:Prefix|} = "v1:"u8.ToArray();

                                  public bool M(ReadOnlySpan<byte> input) => input.StartsWith(Prefix);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private static ReadOnlySpan<byte> Prefix => "v1:"u8;

                                       public bool M(ReadOnlySpan<byte> input) => input.StartsWith(Prefix);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a spread-built field indexed and measured is flagged and becomes a property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpreadFieldWithElementReadsIsFlaggedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private static readonly byte[] {|PSH1013:Marker|} = [.. "ok"u8];

                                  public int M() => Marker.Length + Marker[0];
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private static ReadOnlySpan<byte> Marker => "ok"u8;

                                       public int M() => Marker.Length + Marker[0];
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field passed to a byte-array parameter stays clean; the property would not compile.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayArgumentUsageIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private static readonly byte[] Marker = "ok"u8.ToArray();

                public void M() => Use(Marker);

                private static void Use(byte[] data)
                {
                }
            }
            """);

    /// <summary>Verifies a mutated field stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutatedFieldIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private static readonly byte[] Marker = "ok"u8.ToArray();

                public void M() => Marker[0] = 1;
            }
            """);

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
