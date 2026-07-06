// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyEqualsOverCompare = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1216UseEqualsOverCompareAnalyzer,
    PerformanceSharp.Analyzers.Psh1216UseEqualsOverCompareCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1216 (ask for equality, not ordering) and its code fix.</summary>
public class UseEqualsOverCompareAnalyzerUnitTest
{
    /// <summary>Verifies <c>string.Compare(a, b) == 0</c> is rewritten to a current-culture <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoArgumentCompareBecomesCurrentCultureEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the reversed <c>0 == string.Compare(a, b)</c> operand order is rewritten the same way.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReversedCompareBecomesCurrentCultureEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:0 == string.Compare(left, right)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies <c>string.Compare(a, b) != 0</c> is rewritten to a negated <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareInequalityBecomesNegatedEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right) != 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => !string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an explicit <c>StringComparison</c> argument is carried over unchanged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareWithExplicitComparisonKeepsComparisonAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right, System.StringComparison.OrdinalIgnoreCase) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a literal <c>true</c> ignore-case flag maps to <c>CurrentCultureIgnoreCase</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareWithTrueFlagBecomesCurrentCultureIgnoreCaseAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right, true) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCultureIgnoreCase);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a literal <c>false</c> ignore-case flag maps to <c>CurrentCulture</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareWithFalseFlagBecomesCurrentCultureAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right, false) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies <c>string.CompareOrdinal(a, b) == 0</c> maps to an ordinal <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareOrdinalBecomesOrdinalEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.CompareOrdinal(left, right) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies instance <c>a.CompareTo(b) == 0</c> on strings maps to a current-culture <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareToBecomesCurrentCultureEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:left.CompareTo(right) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies instance <c>a.CompareTo(b) != 0</c> on strings is rewritten to a negated <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareToInequalityBecomesNegatedEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:left.CompareTo(right) != 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => !string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a real ordering test (<c>&gt; 0</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderingComparisonIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => string.Compare(left, right) > 0;
                              }
                              """;
        await VerifyNet90CleanAsync(Source);
    }

    /// <summary>Verifies <c>CompareTo</c> on a non-string receiver is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringCompareToIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(int left, int right)
                                      => left.CompareTo(right) == 0;
                              }
                              """;
        await VerifyNet90CleanAsync(Source);
    }

    /// <summary>Verifies the <c>ignoreCase</c> overload with a non-literal flag stays silent — the flag's
    /// value is unknown, so no <c>StringComparison</c> mapping would be safe.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralIgnoreCaseFlagIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right, bool ignoreCase)
                                      => string.Compare(left, right, ignoreCase) == 0;
                              }
                              """;
        await VerifyNet90CleanAsync(Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyEqualsOverCompare.Test
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
    private static async Task VerifyNet90CleanAsync(string source)
        => await VerifyNet90Async(source, source);
}
