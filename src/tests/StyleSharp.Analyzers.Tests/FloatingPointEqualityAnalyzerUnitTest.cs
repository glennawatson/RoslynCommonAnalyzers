// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFloatingPoint = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1473FloatingPointEqualityAnalyzer,
    StyleSharp.Analyzers.Sst1473FloatingPointEqualityCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1473 (exact floating-point comparison) and its NaN code fix.</summary>
public class FloatingPointEqualityAnalyzerUnitTest
{
    /// <summary>Verifies an exact equality on <c>double</c> and on <c>float</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExactEqualityIsReportedAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool SameDouble(double left, double right) => {|SST1473:left == right|};

                public bool DifferentDouble(double left, double right) => {|SST1473:left != right|};

                public bool SameFloat(float left, float right) => {|SST1473:left == right|};

                public bool Computed(double value) => {|SST1473:(value * 3.0) == 1.0|};
            }
            """);

    /// <summary>Verifies a relational comparison is a legitimate floating-point operation and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RelationalComparisonIsCleanAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Ordered(double left, double right) => left < right;

                public bool AtLeast(double left, double right) => left >= right;

                public bool Near(double left, double right) => System.Math.Abs(left - right) < 1e-9;
            }
            """);

    /// <summary>Verifies <c>decimal</c> is exact and is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecimalIsNeverReportedAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Settled(decimal price, decimal total) => price == total;

                public bool Unsettled(decimal price, decimal total) => price != total;
            }
            """);

    /// <summary>Verifies comparisons of types that do not round are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonFloatingComparisonsAreCleanAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Counted(int left, int right) => left == right;

                public bool Named(string left, string right) => left == right;

                public bool Present(string value) => value != null;

                public bool Set(bool flag) => flag == true;

                public bool Boxed(object left, double right) => left == (object)right;
            }
            """);

    /// <summary>Verifies a comparison against a literal zero tests a sign, not an arithmetic result, and is allowed by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroComparisonIsAllowedByDefaultAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Unset(double value) => value == 0.0;

                public bool Set(double value) => value != 0;

                public bool Empty(float value) => value == 0f;

                public bool Zeroed(double value) => value == 0.000;

                public bool Defaulted(double value) => value == default;

                public bool Scaled(double value) => value == 0e10;
            }
            """);

    /// <summary>Verifies a non-zero literal is still reported even though a zero one is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonZeroLiteralIsStillReportedAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Third(double value) => {|SST1473:value == 0.1|};

                public bool One(float value) => {|SST1473:value != 1f|};
            }
            """);

    /// <summary>Verifies the rule-specific key opts a zero comparison back into the report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroComparisonIsReportedWhenOptedInAsync()
    {
        var test = new VerifyFloatingPoint.Test
        {
            TestCode = """
                       public class C
                       {
                           public bool Unset(double value) => {|SST1473:value == 0.0|};

                           public bool Counted(int value) => value == 0;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1473.allow_zero_comparison = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide key applies when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralZeroComparisonKeyAppliesAsync()
    {
        var test = new VerifyFloatingPoint.Test
        {
            TestCode = """
                       public class C
                       {
                           public bool Unset(float value) => {|SST1473:value != 0f|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.allow_zero_comparison = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific key beats the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificZeroComparisonKeyWinsAsync()
    {
        var test = new VerifyFloatingPoint.Test
        {
            TestCode = """
                       public class C
                       {
                           public bool Unset(double value) => value == 0.0;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.allow_zero_comparison = false
            stylesharp.SST1473.allow_zero_comparison = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable setting falls back to the default rather than reporting every zero comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnparsableZeroComparisonSettingFallsBackAsync()
    {
        var test = new VerifyFloatingPoint.Test
        {
            TestCode = """
                       public class C
                       {
                           public bool Unset(double value) => value == 0.0;

                           public bool Same(double left, double right) => {|SST1473:left == right|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1473.allow_zero_comparison = sometimes

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every operator against NaN is reported, because every one of them answers a constant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryComparisonAgainstNanIsReportedAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Below(double value) => {|SST1473:value < double.NaN|};

                public bool AtMost(double value) => {|SST1473:value <= double.NaN|};

                public bool Above(double value) => {|SST1473:value > double.NaN|};

                public bool AtLeast(double value) => {|SST1473:value >= double.NaN|};

                public bool Reversed(double value) => {|SST1473:double.NaN > value|};

                public bool Both() => {|SST1473:double.NaN == float.NaN|};
            }
            """);

    /// <summary>Verifies a NaN comparison on a nullable operand is reported, even though it has no <c>IsNaN</c> rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableOperandsAreReportedWithoutAFixAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Same(double? left, double? right) => {|SST1473:left == right|};

                                  public bool Missing(double? value) => {|SST1473:value == double.NaN|};
                              }
                              """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a name that reads <c>NaN</c> but belongs to another type is not treated as the framework's NaN.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeignNanFieldIsNotTreatedAsNanAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class Marker
            {
                public const int NaN = -1;
            }

            public class C
            {
                public bool Counted(int value) => value == Marker.NaN;
            }
            """);

    /// <summary>Verifies the general tolerance case is reported but deliberately has no code fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToleranceCaseHasNoCodeFixAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Same(double left, double right) => {|SST1473:left == right|};
                              }
                              """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a comparison whose operands call something is reported but is not treated as a self-comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectingOperandsGetNoFixAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private double _seed;

                                  public double Next() => _seed++;

                                  public bool Stable() => {|SST1473:Next() == Next()|};
                              }
                              """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies <c>x == double.NaN</c> becomes <c>double.IsNaN(x)</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityWithNanBecomesIsNanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Missing(double value) => {|SST1473:value == double.NaN|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool Missing(double value) => double.IsNaN(value);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>x != double.NaN</c> becomes <c>!double.IsNaN(x)</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InequalityWithNanBecomesNegatedIsNanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Present(double value) => {|SST1473:value != double.NaN|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool Present(double value) => !double.IsNaN(value);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the NaN literal is recognized on either side of the operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NanOnTheLeftIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Missing(double value) => {|SST1473:double.NaN == value|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool Missing(double value) => double.IsNaN(value);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a <c>float</c> comparison is rewritten with <c>float.IsNaN</c>, not <c>double.IsNaN</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleComparisonUsesSingleIsNanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Missing(float value) => {|SST1473:value == float.NaN|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool Missing(float value) => float.IsNaN(value);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the self-comparison inversion: <c>x == x</c> is false only for NaN, so it means <c>!IsNaN(x)</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfEqualityBecomesNegatedIsNanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Usable(double value) => {|SST1473:value == value|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool Usable(double value) => !double.IsNaN(value);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the other half of the inversion: <c>x != x</c> is true only for NaN, so it means <c>IsNaN(x)</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfInequalityBecomesIsNanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Missing(double value) => {|SST1473:value != value|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool Missing(double value) => double.IsNaN(value);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a self-comparison of a member-access chain is rewritten against the whole chain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfComparisonOfMemberChainIsFixedAsync()
    {
        const string Source = """
                              public class Point
                              {
                                  public double X { get; set; }
                              }

                              public class C
                              {
                                  private Point _point = new Point();

                                  public bool Usable() => {|SST1473:this._point.X != this._point.X|};
                              }
                              """;
        const string FixedSource = """
                                   public class Point
                                   {
                                       public double X { get; set; }
                                   }

                                   public class C
                                   {
                                       private Point _point = new Point();

                                       public bool Usable() => double.IsNaN(this._point.X);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comparison nested inside a larger condition keeps its place when it is rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedComparisonIsFixedInPlaceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool Ready(double value, bool flag) => flag && {|SST1473:value != double.NaN|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool Ready(double value, bool flag) => flag && !double.IsNaN(value);
                                   }
                                   """;
        await VerifyFloatingPoint.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a widened integer operand still makes the comparison a floating-point one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WidenedOperandIsReportedAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Matches(double value, int count) => {|SST1473:value == count|};

                public bool Reversed(double value) => {|SST1473:3 == value|};
            }
            """);

    /// <summary>Verifies a generic type parameter constrained to a struct is not assumed to be floating point.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericOperandIsCleanAsync()
        => await VerifyFloatingPoint.VerifyAnalyzerAsync(
            """
            public class C<T>
                where T : struct, System.IEquatable<T>
            {
                public bool Same(T left, T right) => left.Equals(right);

                public bool SameObject(object left, object right) => left == right;
            }
            """);
}
