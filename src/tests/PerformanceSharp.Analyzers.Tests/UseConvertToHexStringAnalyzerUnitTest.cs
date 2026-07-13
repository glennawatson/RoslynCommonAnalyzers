// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1224UseConvertToHexStringAnalyzer,
    PerformanceSharp.Analyzers.Psh1224UseConvertToHexStringCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1224UseConvertToHexStringAnalyzer"/> (PSH1224 hand-rolled hex).</summary>
public class UseConvertToHexStringAnalyzerUnitTest
{
    /// <summary>Verifies the hand-rolled hex chain is flagged and collapsed to one call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HandRolledHexIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(byte[] hash) => {|PSH1224:BitConverter.ToString(hash).Replace("-", "")|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(byte[] hash) => Convert.ToHexString(hash);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies string.Empty is recognized as the empty replacement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringEmptyReplacementIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(byte[] hash) => {|PSH1224:BitConverter.ToString(hash).Replace("-", string.Empty)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(byte[] hash) => Convert.ToHexString(hash);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the offset-and-count form carries its arguments across.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OffsetAndCountFormIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(byte[] hash) => {|PSH1224:BitConverter.ToString(hash, 2, 4).Replace("-", "")|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(byte[] hash) => Convert.ToHexString(hash, 2, 4);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a case conversion chained after the call keeps its place, over a cheaper string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The rewrite replaces only the hex-building chain. A lower-casing that followed it still follows
    /// it, so the result is the same text — it is simply produced from one string instead of two.
    /// </remarks>
    [Test]
    public async Task TrailingCaseConversionIsPreservedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(byte[] hash) => {|PSH1224:BitConverter.ToString(hash).Replace("-", "")|}.ToLowerInvariant();
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the separated form kept as-is is not reported — it is a different string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatedHexKeptAsIsIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(byte[] hash) => BitConverter.ToString(hash);
            }
            """);

    /// <summary>Verifies replacing a different separator is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentSeparatorIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(byte[] hash) => BitConverter.ToString(hash).Replace("-", ":");
            }
            """);

    /// <summary>Verifies a ToString on something other than BitConverter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherToStringIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public string M(int value) => value.ToString().Replace("-", "");
            }
            """);

    /// <summary>
    /// Verifies the rule registers nothing against netstandard2.0, where <c>Convert.ToHexString</c>
    /// does not exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The hand-rolled chain is exactly what an author on netstandard2.0 or .NET Framework <i>has</i>
    /// to write, because <c>Convert.ToHexString</c> only arrived in .NET 5. Reporting it there would be
    /// telling them to call something that does not exist, so the method is resolved from the analyzed
    /// compilation and the rule registers no syntax action when it is absent.
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
                           public string M(byte[] hash) => BitConverter.ToString(hash).Replace("-", "");
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
