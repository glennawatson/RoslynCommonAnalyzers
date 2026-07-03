// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCharOverload = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1201UseCharOverloadAnalyzer,
    PerformanceSharp.Analyzers.Psh1201UseCharOverloadCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1201 (use the char overload for single-character strings) and its code fix.</summary>
public class UseCharOverloadAnalyzerUnitTest
{
    /// <summary>Verifies Contains with a single-character literal is reported (PSH1201) and fixed to the char overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsSingleCharacterLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains({|PSH1201:"x"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.Contains('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies StartsWith with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartsWithOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string value)
                                      => value.StartsWith({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.StartsWith('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies EndsWith with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EndsWithOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string value)
                                      => value.EndsWith({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.EndsWith('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies IndexOf with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexOfOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string value)
                                      => value.IndexOf({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string value)
                                           => value.IndexOf('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies LastIndexOf with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LastIndexOfOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string value)
                                      => value.LastIndexOf({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string value)
                                           => value.LastIndexOf('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies bare IndexOf(string) is not reported — it is culture-sensitive, the char overload is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexOfWithoutComparisonIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string value)
                                      => value.IndexOf("x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies bare StartsWith(string) is not reported — it is culture-sensitive, the char overload is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartsWithWithoutComparisonIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.StartsWith("x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a non-ordinal comparison argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartsWithOrdinalIgnoreCaseIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string value)
                                      => value.StartsWith("x", StringComparison.OrdinalIgnoreCase);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a multi-character literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsMultiCharacterLiteralIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains("xy");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a single-quote literal is fixed with correct char escaping.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsSingleQuoteLiteralEscapedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains({|PSH1201:"'"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.Contains('\'');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a verbatim literal is not reported — the rule is scoped to plain literals.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsVerbatimLiteralIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains(@"x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the rule stays silent where string.Contains(char) does not exist (.NET Framework 4.7.2).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsSilentWhereCharOverloadUnavailableAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains("x");
                              }
                              """;

        var test = new VerifyCharOverload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source,
            FixedCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies (where the char overloads exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyCharOverload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
