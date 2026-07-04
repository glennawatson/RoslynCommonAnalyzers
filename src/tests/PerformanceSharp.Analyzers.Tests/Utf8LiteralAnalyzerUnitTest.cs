// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1208Utf8LiteralAnalyzer,
    PerformanceSharp.Analyzers.Psh1208Utf8LiteralCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1208Utf8LiteralAnalyzer"/> (PSH1208 u8 literals).</summary>
public class Utf8LiteralAnalyzerUnitTest
{
    /// <summary>Verifies a constant GetBytes producing an array is flagged and rewritten with ToArray.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetBytesToArrayFieldIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Text;

                              public class C
                              {
                                  private static readonly byte[] Marker = {|PSH1208:Encoding.UTF8.GetBytes("v1:")|};

                                  public int M() => Marker.Length;
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public class C
                                   {
                                       private static readonly byte[] Marker = "v1:"u8.ToArray();

                                       public int M() => Marker.Length;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a GetBytes converted to a span is flagged and becomes the bare literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetBytesAsSpanBecomesBareLiteralAsync()
    {
        const string Source = """
                              using System;
                              using System.Text;

                              public class C
                              {
                                  public bool M(ReadOnlySpan<byte> input)
                                  {
                                      ReadOnlySpan<byte> prefix = {|PSH1208:Encoding.UTF8.GetBytes("v1:")|};
                                      return input.StartsWith(prefix);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Text;

                                   public class C
                                   {
                                       public bool M(ReadOnlySpan<byte> input)
                                       {
                                           ReadOnlySpan<byte> prefix = "v1:"u8;
                                           return input.StartsWith(prefix);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a non-constant argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public byte[] M(string value) => Encoding.UTF8.GetBytes(value);
            }
            """);

    /// <summary>Verifies an ASCII receiver with a non-ASCII constant stays clean; the bytes would change.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsciiEncodingWithNonAsciiConstantIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public byte[] M() => Encoding.ASCII.GetBytes("café");
            }
            """);

    /// <summary>Verifies an older language version stays clean; u8 literals need C# 11.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OlderLanguageVersionIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Text;

                       public class C
                       {
                           public byte[] M() => Encoding.UTF8.GetBytes("v1:");
                       }
                       """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp10)));
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
