// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1210Utf8SequenceEqualAnalyzer,
    PerformanceSharp.Analyzers.Psh1210Utf8SequenceEqualCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1210Utf8SequenceEqualAnalyzer"/> (PSH1210 UTF-8 byte comparison).</summary>
public class Utf8SequenceEqualAnalyzerUnitTest
{
    /// <summary>Verifies a decoded array comparison is flagged and rewritten through AsSpan.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayDecodeComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Text;

                              public class C
                              {
                                  public bool M(byte[] payload) => {|PSH1210:Encoding.UTF8.GetString(payload) == "ok"|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Text;

                                   public class C
                                   {
                                       public bool M(byte[] payload) => payload.AsSpan().SequenceEqual("ok"u8);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a span decode inequality is flagged and gains a negation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanDecodeInequalityIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Text;

                              public class C
                              {
                                  public bool M(ReadOnlySpan<byte> payload) => {|PSH1210:"ok" != Encoding.UTF8.GetString(payload)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Text;

                                   public class C
                                   {
                                       public bool M(ReadOnlySpan<byte> payload) => !payload.SequenceEqual("ok"u8);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies comparing two decoded strings stays clean; only constants qualify.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantComparisonIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public bool M(byte[] payload, string expected) => Encoding.UTF8.GetString(payload) == expected;
            }
            """);

    /// <summary>Verifies a constant containing the replacement character stays clean; invalid decodes could alias it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReplacementCharacterConstantIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public bool M(byte[] payload) => Encoding.UTF8.GetString(payload) == "�";
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
