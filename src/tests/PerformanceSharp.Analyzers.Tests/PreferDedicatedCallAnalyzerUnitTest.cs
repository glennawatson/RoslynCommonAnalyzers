// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1227PreferDedicatedCallAnalyzer,
    PerformanceSharp.Analyzers.Psh1227PreferDedicatedCallCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1227PreferDedicatedCallAnalyzer"/> (PSH1227 use the purpose-built call).</summary>
public class PreferDedicatedCallAnalyzerUnitTest
{
    /// <summary>Verifies an ordinal string.Compare is reported and rewritten to string.CompareOrdinal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinalCompareIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string a, string b) => {|PSH1227:string.Compare(a, b, StringComparison.Ordinal)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string a, string b) => string.CompareOrdinal(a, b);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an always-false Debug.Assert with a message is reported and rewritten to Debug.Fail.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlwaysFalseAssertIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Diagnostics;

                              public class C
                              {
                                  public void M(string message)
                                  {
                                      {|PSH1227:Debug.Assert(false, message)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics;

                                   public class C
                                   {
                                       public void M(string message)
                                       {
                                           Debug.Fail(message);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the detail-message Debug.Assert overload is rewritten to the matching Debug.Fail overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlwaysFalseAssertWithDetailIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Diagnostics;

                              public class C
                              {
                                  public void M(string message, string detail)
                                  {
                                      {|PSH1227:Debug.Assert(false, message, detail)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics;

                                   public class C
                                   {
                                       public void M(string message, string detail)
                                       {
                                           Debug.Fail(message, detail);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a case-insensitive ordinal comparison is not reported: CompareOrdinal is case-sensitive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinalIgnoreCaseCompareIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public int M(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
            """);

    /// <summary>Verifies an ordinal Compare tested against zero is left to the equality-versus-ordering rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrdinalCompareAgainstZeroIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public bool M(string a, string b) => string.Compare(a, b, StringComparison.Ordinal) == 0;
            }
            """);

    /// <summary>Verifies a two-argument string.Compare without a StringComparison is not this rule's shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CultureCompareIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public int M(string a, string b) => string.Compare(a, b);
            }
            """);

    /// <summary>Verifies an assertion of a real condition is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RealConditionAssertIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(bool condition, string message) => Debug.Assert(condition, message);
            }
            """);

    /// <summary>Verifies a message-less Debug.Assert(false) is not reported: Debug.Fail needs a message.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MessagelessAssertIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M() => Debug.Assert(false);
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyAsync(source, source);
}
