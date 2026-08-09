// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDifferenceComparedToZero = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2447DifferenceComparedToZeroAnalyzer,
    StyleSharp.Analyzers.Sst2447DifferenceComparedToZeroCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2447DifferenceComparedToZeroAnalyzer"/> and its code fix (SST2447).</summary>
public class DifferenceComparedToZeroAnalyzerUnitTest
{
    /// <summary>Verifies a greater-than difference is reported and rewritten to a direct comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GreaterThanDifferenceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:a - b > 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a > b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mirrored comparison flips the operator when the operands change sides.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MirroredComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:0 < a - b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a > b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an equality test against zero becomes an equality test of the operands.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityDifferenceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(long a, long b) => {|SST2447:a - b == 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(long a, long b) => a == b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inequality test against zero becomes an inequality test of the operands.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InequalityDifferenceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:a - b != 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a != b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a less-than-or-equal difference keeps its operator with the subtraction on the left.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LessThanOrEqualDifferenceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:a - b <= 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a <= b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mirrored greater-than-or-equal flips to less-than-or-equal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MirroredGreaterThanOrEqualIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:0 >= a - b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a <= b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a parenthesized difference is reported and unwrapped by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedDifferenceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(uint a, uint b) => {|SST2447:(a - b) > 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(uint a, uint b) => a > b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mirrored less-than flips to greater-than.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MirroredLessThanIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:0 > a - b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a < b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a less-than difference keeps its operator with the subtraction on the left.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LessThanDifferenceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:a - b < 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a < b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a greater-than-or-equal difference keeps its operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GreaterThanOrEqualDifferenceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:a - b >= 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a >= b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mirrored less-than-or-equal flips to greater-than-or-equal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MirroredLessThanOrEqualIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(int a, int b) => {|SST2447:0 <= a - b|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(int a, int b) => a >= b;
                                   }
                                   """;
        await VerifyDifferenceComparedToZero.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a floating-point difference is left alone; it has no wrapping difference to misread.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FloatingPointDifferenceIsCleanAsync()
        => await VerifyDifferenceComparedToZero.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(double a, double b) => a - b > 0;
            }
            """);

    /// <summary>Verifies a comparison against something other than zero is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonAgainstNonZeroIsCleanAsync()
        => await VerifyDifferenceComparedToZero.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(int a, int b) => a - b > 1;
            }
            """);

    /// <summary>Verifies a comparison of something other than a difference is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonOfNonDifferenceIsCleanAsync()
        => await VerifyDifferenceComparedToZero.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(int a, int b) => a + b > 0;
            }
            """);

    /// <summary>Verifies a decimal difference is left alone; decimal arithmetic throws rather than wrapping.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecimalDifferenceIsCleanAsync()
        => await VerifyDifferenceComparedToZero.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(decimal a, decimal b) => a - b > 0;
            }
            """);
}
