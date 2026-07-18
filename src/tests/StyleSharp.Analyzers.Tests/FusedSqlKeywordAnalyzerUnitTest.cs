// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifySql = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2470FusedSqlKeywordAnalyzer>;
using VerifySqlFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2470FusedSqlKeywordAnalyzer,
    StyleSharp.Analyzers.Sst2470FusedSqlKeywordCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2470 (two string literals whose seam fuses a SQL keyword).</summary>
public class FusedSqlKeywordAnalyzerUnitTest
{
    /// <summary>Verifies a keyword fused onto the left operand's tail is reported and spaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingKeywordFusedOntoLeftTailIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string Q() => {|SST2470:"SELECT * FROM t" + "WHERE id = 1"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string Q() => "SELECT * FROM t" + " WHERE id = 1";
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a keyword fused onto a trailing digit is reported and spaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingKeywordFusedOntoDigitIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string Q() => {|SST2470:"SELECT * FROM t WHERE a = 1" + "AND b = 2"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string Q() => "SELECT * FROM t WHERE a = 1" + " AND b = 2";
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the symmetric case — a trailing keyword fused onto the right operand's head.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingKeywordFusedOntoRightHeadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string Q() => {|SST2470:"SELECT id FROM" + "users"|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string Q() => "SELECT id FROM" + " users";
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a verbatim left operand with a regular right operand is still reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerbatimLeftWithRegularRightIsFlaggedAndFixedAsync()
    {
        const string Source = """"
                              public class C
                              {
                                  public string Q() => {|SST2470:@"SELECT * FROM t" + "WHERE id = 1"|};
                              }
                              """";
        const string FixedSource = """"
                                   public class C
                                   {
                                       public string Q() => @"SELECT * FROM t" + " WHERE id = 1";
                                   }
                                   """";
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a verbatim right operand is reported but not fixed, leaving the seam to the author.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VerbatimRightOperandIsReportedWithoutFixAsync()
        => await VerifyReportAsync(
            """"
            public class C
            {
                public string Q() => {|SST2470:"SELECT * FROM t" + @"WHERE id = 1"|};
            }
            """");

    /// <summary>Verifies a raw-string right operand is reported but not fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RawStringRightOperandIsReportedWithoutFixAsync()
        => await VerifyReportAsync(
            """"
            public class C
            {
                public string Q() => {|SST2470:"SELECT * FROM t" + """WHERE id = 1"""|};
            }
            """");

    /// <summary>Verifies a space at the seam is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpaceAtSeamIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Q() => "SELECT * FROM t" + " WHERE id = 1";
            }
            """);

    /// <summary>Verifies a punctuation token boundary at the seam is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PunctuationBoundaryAtSeamIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Q() => "SELECT a," + "FROM t";
            }
            """);

    /// <summary>Verifies a keyword that is only a prefix of a longer word is never treated as fused.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeywordPrefixOfLongerWordIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Q() => "SELECT * FROM t" + "WHEREVER it goes";
            }
            """);

    /// <summary>Verifies prose whose left side is not SQL is never reported even when the right starts with a weak keyword.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProseWithWeakKeywordIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Q() => "click here" + "OR press escape";
            }
            """);

    /// <summary>Verifies an interpolated operand is never a candidate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedOperandIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Q(string t) => $"SELECT * FROM {t}" + "WHERE id = 1";
            }
            """);

    /// <summary>Verifies an empty operand is never a candidate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyOperandIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Q() => "" + "WHERE id = 1";
            }
            """);

    /// <summary>Verifies a non-literal right operand is never a candidate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralOperandIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Q(string clause) => "SELECT * FROM t" + clause;
            }
            """);

    /// <summary>Runs a report-and-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifySqlFix.Test
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
        var test = new VerifySql.Test
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
