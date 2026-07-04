// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1212AsSpanOverSubstringAnalyzer,
    PerformanceSharp.Analyzers.Psh1212AsSpanOverSubstringCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1212AsSpanOverSubstringAnalyzer"/> (PSH1212 AsSpan slices).</summary>
public class AsSpanOverSubstringAnalyzerUnitTest
{
    /// <summary>Verifies a Substring argument with a span overload is flagged and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringWithSpanOverloadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value) => Use({|PSH1212:value.Substring(1)|});

                                  private static void Use(string text)
                                  {
                                  }

                                  private static void Use(ReadOnlySpan<char> text)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value) => Use(value.AsSpan(1));

                                       private static void Use(string text)
                                       {
                                       }

                                       private static void Use(ReadOnlySpan<char> text)
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the two-argument slice carries both arguments through the rename.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoArgumentSubstringIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value) => Use({|PSH1212:value.Substring(1, 3)|});

                                  private static void Use(string text)
                                  {
                                  }

                                  private static void Use(ReadOnlySpan<char> text)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value) => Use(value.AsSpan(1, 3));

                                       private static void Use(string text)
                                       {
                                       }

                                       private static void Use(ReadOnlySpan<char> text)
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a consumer without a span overload stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoSpanOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(string value) => Use(value.Substring(1));

                private static void Use(string text)
                {
                }
            }
            """);

    /// <summary>Verifies a Substring outside an argument position stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StandaloneSubstringIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(string value) => value.Substring(1);
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
