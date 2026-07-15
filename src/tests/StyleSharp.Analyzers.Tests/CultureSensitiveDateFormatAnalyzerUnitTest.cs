// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyDate = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2445CultureSensitiveDateFormatAnalyzer>;
using VerifyDateFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2445CultureSensitiveDateFormatAnalyzer,
    StyleSharp.Analyzers.Sst2445CultureSensitiveDateFormatCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2445 (a culture-sensitive custom date/time format).</summary>
public class CultureSensitiveDateFormatAnalyzerUnitTest
{
    /// <summary>Verifies a current-culture ToString is reported and the separators can be quoted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringQuoteFixAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;

                              public class C
                              {
                                  public string Show(DateTime d) => d.ToString({|SST2445:"dd/MM/yyyy"|}, CultureInfo.CurrentCulture);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;

                                   public class C
                                   {
                                       public string Show(DateTime d) => d.ToString("dd'/'MM'/'yyyy", CultureInfo.CurrentCulture);
                                   }
                                   """;
        await VerifyQuoteAsync(Source, FixedSource);
    }

    /// <summary>Verifies a current-culture ToString can switch to the invariant culture.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToStringInvariantFixAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;

                              public class C
                              {
                                  public string Show(DateTime d) => d.ToString({|SST2445:"dd/MM/yyyy"|}, CultureInfo.CurrentCulture);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;

                                   public class C
                                   {
                                       public string Show(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                                   }
                                   """;
        await VerifyInvariantAsync(Source, FixedSource);
    }

    /// <summary>Verifies a time separator is reported and quoted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TimeSeparatorQuoteFixAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;

                              public class C
                              {
                                  public string Show(DateTime d) => d.ToString({|SST2445:"HH:mm:ss"|}, CultureInfo.CurrentCulture);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;

                                   public class C
                                   {
                                       public string Show(DateTime d) => d.ToString("HH':'mm':'ss", CultureInfo.CurrentCulture);
                                   }
                                   """;
        await VerifyQuoteAsync(Source, FixedSource);
    }

    /// <summary>Verifies a current-UI-culture provider is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CurrentUiCultureIsFlaggedAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;

                              public class C
                              {
                                  public string Show(DateTime d) => d.ToString({|SST2445:"dd/MM/yyyy"|}, CultureInfo.CurrentUICulture);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;

                                   public class C
                                   {
                                       public string Show(DateTime d) => d.ToString("dd'/'MM'/'yyyy", CultureInfo.CurrentUICulture);
                                   }
                                   """;
        await VerifyQuoteAsync(Source, FixedSource);
    }

    /// <summary>Verifies a current-culture ParseExact is reported and quoted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParseExactQuoteFixAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;

                              public class C
                              {
                                  public DateTime Read(string s) => DateTime.ParseExact(s, {|SST2445:"dd/MM/yyyy"|}, CultureInfo.CurrentCulture);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;

                                   public class C
                                   {
                                       public DateTime Read(string s) => DateTime.ParseExact(s, "dd'/'MM'/'yyyy", CultureInfo.CurrentCulture);
                                   }
                                   """;
        await VerifyQuoteAsync(Source, FixedSource);
    }

    /// <summary>Verifies a DateOnly ToString is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DateOnlyIsFlaggedAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;

                              public class C
                              {
                                  public string Show(DateOnly d) => d.ToString({|SST2445:"dd/MM/yyyy"|}, CultureInfo.CurrentCulture);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;

                                   public class C
                                   {
                                       public string Show(DateOnly d) => d.ToString("dd'/'MM'/'yyyy", CultureInfo.CurrentCulture);
                                   }
                                   """;
        await VerifyQuoteAsync(Source, FixedSource);
    }

    /// <summary>Verifies a plain interpolated string is reported and its separators quoted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedStringQuoteFixAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string Show(DateTime d) => $"{d:{|SST2445:dd/MM/yyyy|}}";
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string Show(DateTime d) => $"{d:dd'/'MM'/'yyyy}";
                                   }
                                   """;
        await VerifyQuoteAsync(Source, FixedSource);
    }

    /// <summary>Verifies the invariant provider is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvariantProviderIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;
            using System.Globalization;

            public class C
            {
                public string Show(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            """);

    /// <summary>Verifies the no-provider overload is out of scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoProviderIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public string Show(DateTime d) => d.ToString("dd/MM/yyyy");
            }
            """);

    /// <summary>Verifies an already-quoted separator is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QuotedSeparatorIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;
            using System.Globalization;

            public class C
            {
                public string Show(DateTime d) => d.ToString("dd'/'MM'/'yyyy", CultureInfo.CurrentCulture);
            }
            """);

    /// <summary>Verifies a standard specifier is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StandardSpecifierIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;
            using System.Globalization;

            public class C
            {
                public string Sortable(DateTime d) => d.ToString("O", CultureInfo.CurrentCulture);

                public string RoundTrip(DateTime d) => d.ToString("s", CultureInfo.CurrentCulture);
            }
            """);

    /// <summary>Verifies a literal dot separator is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralDotIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;
            using System.Globalization;

            public class C
            {
                public string Show(DateTime d) => d.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
            }
            """);

    /// <summary>Verifies an interpolated string built invariantly is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvariantInterpolationIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;
            using System.Globalization;

            public class C
            {
                public string Show(DateTime d) => string.Create(CultureInfo.InvariantCulture, $"{d:dd/MM/yyyy}");
            }
            """);

    /// <summary>Verifies an interpolation over a non-date value is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonDateInterpolationIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Show(int ratio) => $"{ratio:0/0}";
            }
            """);

    /// <summary>Runs the quote fix against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyQuoteAsync(string source, string fixedSource)
    {
        var test = new VerifyDateFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
            CodeActionEquivalenceKey = "SST2445.Quote",
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the invariant-culture fix against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyInvariantAsync(string source, string fixedSource)
    {
        var test = new VerifyDateFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
            CodeActionEquivalenceKey = "SST2445.Invariant",
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source)
    {
        var test = new VerifyDate.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
