// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCaseConversion = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1200AvoidCaseConversionComparisonAnalyzer,
    PerformanceSharp.Analyzers.Psh1200AvoidCaseConversionComparisonCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1200 (compare strings without allocating case-converted copies) and its code fix.</summary>
public class AvoidCaseConversionComparisonAnalyzerUnitTest
{
    /// <summary>Verifies <c>==</c> with both sides lower-cased is reported (PSH1200) and rewritten to string.Equals.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityWithBothToLowerReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.ToLower() {|PSH1200:==|} right.ToLower();
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

    /// <summary>Verifies <c>!=</c> with both sides upper-cased is rewritten to a negated string.Equals.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InequalityWithBothToUpperReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.ToUpper() {|PSH1200:!=|} right.ToUpper();
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => !string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the invariant conversions map to InvariantCultureIgnoreCase.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvariantConversionUsesInvariantCultureIgnoreCaseAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.ToLowerInvariant() {|PSH1200:==|} right.ToLowerInvariant();
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.InvariantCultureIgnoreCase);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies instance Equals with both sides lower-cased is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceEqualsWithBothToLowerReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.ToLower().{|PSH1200:Equals|}(right.ToLower());
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

    /// <summary>Verifies static string.Equals with both arguments lower-cased is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticEqualsWithBothToLowerReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => string.{|PSH1200:Equals|}(left.ToLower(), right.ToLower());
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

    /// <summary>Verifies a single-sided conversion against a literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleSidedConversionIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left)
                                      => left.ToLower() == "abc";
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies mismatched conversion methods on the two sides are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedConversionMethodsAreCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => left.ToLower() == right.ToUpper();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a non-string receiver with its own ToLower method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringReceiverIsCleanAsync()
    {
        const string Source = """
                              public class Wrapper
                              {
                                  public string ToLower() => "x";
                              }

                              public class C
                              {
                                  public bool M(Wrapper left, Wrapper right)
                                      => left.ToLower() == right.ToLower();
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyCaseConversion.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
