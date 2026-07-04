// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1211RemoveIntermediateToStringAnalyzer,
    PerformanceSharp.Analyzers.Psh1211RemoveIntermediateToStringCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1211RemoveIntermediateToStringAnalyzer"/> (PSH1211 intermediate ToString).</summary>
public class RemoveIntermediateToStringAnalyzerUnitTest
{
    /// <summary>Verifies a ToString argument with a direct overload is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentWithDirectOverloadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int value) => Console.Write({|PSH1211:value.ToString()|});
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int value) => Console.Write(value);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ToString inside an interpolation hole is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolationHoleIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(int count) => $"{{|PSH1211:count.ToString()|}} items";
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(int count) => $"{count} items";
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ToString with a format argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormattedToStringIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public void M(int value) => Console.Write(value.ToString("X"));
            }
            """);

    /// <summary>Verifies a ToString feeding a method without a direct overload stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoDirectOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(System.Guid id) => Use(id.ToString());

                private static void Use(string text)
                {
                }
            }
            """);

    /// <summary>Verifies a single-hole interpolation wrapper stays clean; PSH1205 owns that shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleHoleInterpolationIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(int count) => $"{count.ToString()}";
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
