// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDivision = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1477IntegerDivisionAsFloatingPointAnalyzer,
    StyleSharp.Analyzers.Sst1477IntegerDivisionAsFloatingPointCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1477 (integer division widened to a floating-point target) and its fix.</summary>
public class IntegerDivisionAsFloatingPointAnalyzerUnitTest
{
    /// <summary>Verifies a division widened to a double is reported and one that stays integral is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WideningToDoubleIsReportedAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public double Average(int total, int count) => {|SST1477:total / count|};

                public int Pages(int total, int size) => total / size;
            }
            """);

    /// <summary>Verifies every context that widens the truncated quotient is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The rule does not enumerate these shapes; each of them is the same fact in the semantic model, so
    /// this test is really checking that the one question the analyzer asks covers all of them.
    /// </remarks>
    [Test]
    public async Task EveryWideningContextIsReportedAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly double _seed = {|SST1477:1 / 2|};

                public double Seed => _seed;

                public double Initializer(int a, int b)
                {
                    double value = {|SST1477:a / b|};
                    return value;
                }

                public double Compound(int a, int b)
                {
                    double value = 0.0;
                    value += {|SST1477:a / b|};
                    return value;
                }

                public double Returned(int a, int b) => {|SST1477:a / b|};

                public double Argument(int a, int b) => System.Math.Sqrt({|SST1477:a / b|});

                public double Branch(bool flag, int a, int b) => flag ? {|SST1477:a / b|} : 0.0;

                public double Parenthesized(int a, int b) => ({|SST1477:a / b|});

                public double Negated(int a, int b) => -({|SST1477:a / b|});
            }
            """);

    /// <summary>Verifies a float and a decimal target are reported and cast just like a double one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FloatAndDecimalTargetsAreReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public float Fraction(int part, int whole) => {|SST1477:part / whole|};

                                  public decimal Rate(int amount, int months) => {|SST1477:amount / months|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public float Fraction(int part, int whole) => (float)part / whole;

                                       public decimal Rate(int amount, int months) => (decimal)amount / months;
                                   }
                                   """;
        await VerifyDivision.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an unsigned and a long division are reported, since both truncate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryIntegralOperandTypeIsReportedAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public double Unsigned(uint a, uint b) => {|SST1477:a / b|};

                public double Wide(long a, long b) => {|SST1477:a / b|};

                public double Narrow(byte a, byte b) => {|SST1477:a / b|};
            }
            """);

    /// <summary>Verifies a hexadecimal divisor is still an integer, even when its digits look like an exponent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The literal check that clears <c>value / 2e1</c> without the semantic model must not clear
    /// <c>value / 0xE</c>, whose 'E' is a digit rather than an exponent.
    /// </remarks>
    [Test]
    public async Task HexadecimalDivisorIsReportedAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public double Hex(int value) => {|SST1477:value / 0xE|};

                public double Exponent(int value) => value / 2e1;
            }
            """);

    /// <summary>Verifies a division with a floating-point operand already divides exactly and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FloatingPointOperandIsCleanAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public double Half(int value) => value / 2.0;

                public double Suffixed(int value) => value / 2f;

                public double PromotedLeft(int total, int count) => (double)total / count;

                public double PromotedRight(int total, int count) => total / (double)count;

                public decimal Money(decimal amount, int months) => amount / months;

                public double Real(double a, int b) => a / b;
            }
            """);

    /// <summary>Verifies a division whose result stays integral is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntegralContextIsCleanAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Pages(int total, int size) => total / size;

                public long Chunks(long total, long size) => total / size;

                public int Truncated(int a, int b) => (int)(a / b);

                public void Discarded(int a, int b) => System.Console.WriteLine(a / b);
            }
            """);

    /// <summary>Verifies a division consumed by an integral operation is left to the operation that widens.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// In <c>(a / b) + 1</c> the widening applies to the sum, not to the division, so the division is not
    /// reported. This is the rule's deliberate boundary: it reports the expression the conversion is
    /// actually attached to, which keeps it precise and keeps it from guessing at arithmetic it cannot see
    /// the end of.
    /// </remarks>
    [Test]
    public async Task TruncationInsideAnIntegralExpressionIsNotReportedAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public double Offset(int a, int b) => (a / b) + 1;
            }
            """);

    /// <summary>Verifies a nullable division is not reported, since the lifted operator has no special type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableOperandsAreNotReportedAsync()
        => await VerifyDivision.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public double? Maybe(int? a, int? b) => a / b;
            }
            """);

    /// <summary>Verifies the fix casts the left operand, which promotes the whole division.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixCastsTheLeftOperandAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public double Average(int total, int count) => {|SST1477:total / count|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public double Average(int total, int count) => (double)total / count;
                                   }
                                   """;
        await VerifyDivision.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix keeps an operand that is itself an operation grouped under the cast.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>(double)a * b / c</c> would multiply in floating point, which is a different computation;
    /// <c>(double)(a * b) / c</c> keeps the integer multiply and only the division changes.
    /// </remarks>
    [Test]
    public async Task CodeFixParenthesizesACompoundLeftOperandAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public double Scaled(int a, int b, int c) => {|SST1477:a * b / c|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public double Scaled(int a, int b, int c) => (double)(a * b) / c;
                                   }
                                   """;
        await VerifyDivision.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix promotes a negated division without disturbing the sign.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixPromotesANegatedDivisionAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public double Negated(int a, int b) => -({|SST1477:a / b|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public double Negated(int a, int b) => -((double)a / b);
                                   }
                                   """;
        await VerifyDivision.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an explicit cast around the division is reported, since it truncates before it widens.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The fix replaces the cast instead of nesting a second one inside it.</remarks>
    [Test]
    public async Task CodeFixReplacesAnExplicitCastAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public double Average(int total, int count) => (double)({|SST1477:total / count|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public double Average(int total, int count) => (double)total / count;
                                   }
                                   """;
        await VerifyDivision.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a replaced cast keeps its grouping when an operator could bind to the division.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A cast binds tighter than <c>*</c> but a division does not, so <c>2.0 * (double)a / c</c> would
    /// regroup as <c>(2.0 * (double)a) / c</c>; the parentheses keep the division whole.
    /// </remarks>
    [Test]
    public async Task CodeFixParenthesizesAReplacedCastUnderAnOperatorAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public double Scaled(int total, int count) => 2.0 * (double)({|SST1477:total / count|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public double Scaled(int total, int count) => 2.0 * ((double)total / count);
                                   }
                                   """;
        await VerifyDivision.VerifyCodeFixAsync(Source, FixedSource);
    }
}
