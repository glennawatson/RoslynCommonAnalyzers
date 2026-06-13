// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInverted = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.InvertedBooleanCheckCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1172 (boolean comparisons should not be inverted) and its fix.</summary>
public class InvertedBooleanCheckAnalyzerUnitTest
{
    /// <summary>Verifies each inverted comparison is reported and rewritten to the opposite operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvertedComparisonsRewrittenAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool E1(int a, int b) => {|SST1172:!(a == b)|};

                                  public bool E2(int a, int b) => {|SST1172:!(a != b)|};

                                  public bool R1(int a, int b) => {|SST1172:!(a < b)|};

                                  public bool R2(int a, int b) => {|SST1172:!(a <= b)|};

                                  public bool R3(int a, int b) => {|SST1172:!(a > b)|};

                                  public bool R4(int a, int b) => {|SST1172:!(a >= b)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool E1(int a, int b) => a != b;

                                       public bool E2(int a, int b) => a == b;

                                       public bool R1(int a, int b) => a >= b;

                                       public bool R2(int a, int b) => a > b;

                                       public bool R3(int a, int b) => a <= b;

                                       public bool R4(int a, int b) => a < b;
                                   }
                                   """;
        await VerifyInverted.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies equality inversion is reported even for reference and nullable operands (it is always safe).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityInversionAlwaysReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(int? a, int? b) => {|SST1172:!(a == b)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(int? a, int? b) => a != b;
                                   }
                                   """;
        await VerifyInverted.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies relational inversion on nullable operands is not reported (it does not preserve null).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RelationalInversionOnNullableIsCleanAsync()
        => await VerifyInverted.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool M(int? a, int? b) => !(a < b);
            }
            """);

    /// <summary>Verifies relational inversion on floating-point operands is not reported (it does not preserve NaN).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RelationalInversionOnFloatingPointIsCleanAsync()
        => await VerifyInverted.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool M(double a, double b) => !(a < b);
            }
            """);

    /// <summary>Verifies a logical-not over a non-comparison (logical operator, plain operand) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonComparisonNegationIsCleanAsync()
        => await VerifyInverted.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool M(bool a, bool b) => !(a && b);

                public bool N(bool a) => !a;
            }
            """);
}
