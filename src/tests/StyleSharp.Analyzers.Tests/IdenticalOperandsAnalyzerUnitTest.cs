// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIdenticalOperands = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1474IdenticalOperandsAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1474 (identical expressions on both sides of an operator).</summary>
public class IdenticalOperandsAnalyzerUnitTest
{
    /// <summary>Verifies every reported operator is reported when its operands are the same.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryMeaninglessOperatorIsReportedAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Same(int value) => {|SST1474:value == value|};

                public bool NotSame(int value) => {|SST1474:value != value|};

                public bool Less(int value) => {|SST1474:value < value|};

                public bool AtMost(int value) => {|SST1474:value <= value|};

                public bool More(int value) => {|SST1474:value > value|};

                public bool AtLeast(int value) => {|SST1474:value >= value|};

                public bool Both(bool flag) => {|SST1474:flag && flag|};

                public bool Either(bool flag) => {|SST1474:flag || flag|};

                public int Mask(int value) => {|SST1474:value & value|};

                public int Merge(int value) => {|SST1474:value | value|};

                public int Toggle(int value) => {|SST1474:value ^ value|};

                public int Zero(int value) => {|SST1474:value - value|};

                public int Unit(int value) => {|SST1474:value / value|};

                public int Remainder(int value) => {|SST1474:value % value|};
            }
            """);

    /// <summary>Verifies doubling and squaring are legitimate and are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdditionAndMultiplicationAreCleanAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Double(int value) => value + value;

                public int Square(int value) => value * value;

                public int Shift(int value) => value << value;
            }
            """);

    /// <summary>Verifies different operands are not reported, which is the path almost every operator takes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentOperandsAreCleanAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _left;
                private int _right;

                public bool Same(int left, int right) => left == right;

                public bool Fields() => _left < _right;

                public int Difference(int left, int right) => left - right;

                public int Half(int value) => value / 2;

                public bool Both(bool left, bool right) => left && right;
            }
            """);

    /// <summary>Verifies an operand that calls something is two different evaluations, not one expression twice.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectingOperandsAreCleanAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _seed;

                private int[] _values = new int[4];

                public int Next() => _seed++;

                public bool Advance() => Next() == Next();

                public bool Chained() => this.Next() == this.Next();

                public bool Increment(int value) => ++value == ++value;

                public bool Created() => new C() == new C();

                public bool Indexed(int index) => _values[index] == _values[index];

                public bool Nested(int value) => (Next() + value) == (Next() + value);
            }
            """);

    /// <summary>Verifies an await in an operand disqualifies the comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedOperandsAreCleanAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task<int> ReadAsync() => Task.FromResult(1);

                public async Task<bool> CompareAsync() => await ReadAsync() == await ReadAsync();
            }
            """);

    /// <summary>Verifies a self-comparison of a member-access chain is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessChainIsReportedAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class Inner
            {
                public int Depth { get; set; }
            }

            public class Outer
            {
                public Inner Child { get; set; } = new Inner();
            }

            public class C
            {
                private Outer _outer = new Outer();

                public bool Same() => {|SST1474:_outer.Child.Depth == _outer.Child.Depth|};

                public bool Qualified() => {|SST1474:this._outer.Child.Depth >= this._outer.Child.Depth|};

                public bool Different(Outer other) => _outer.Child.Depth == other.Child.Depth;
            }
            """);

    /// <summary>Verifies trivia and parentheses do not hide a self-comparison, and that pure operator trees are compared.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructurallyEquivalentOperandsAreReportedAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Spaced(int left, int right) => {|SST1474:(left + right) == (left  +  right)|};

                public bool Negated(bool flag) => {|SST1474:!flag && !flag|};

                public int Cast(long value) => {|SST1474:(int)value - (int)value|};
            }
            """);

    /// <summary>Verifies a floating-point equality is left to SST1473, which owns the NaN idiom and its fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FloatingPointEqualityIsDeferredToTheFloatingPointRuleAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool NanIdiom(double value) => value != value;

                public bool Usable(double value) => value == value;

                public bool SingleNanIdiom(float value) => value != value;

                public bool NullableNanIdiom(double? value) => value != value;
            }
            """);

    /// <summary>Verifies the deferral is limited to equality: an ordering or an arithmetic operator on a double is still nonsense.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEqualityFloatingPointOperatorsAreStillReportedAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Ordered(double value) => {|SST1474:value < value|};

                public double Zero(double value) => {|SST1474:value - value|};

                public double Unit(double value) => {|SST1474:value / value|};
            }
            """);

    /// <summary>Verifies a decimal self-comparison is reported here, because decimal is exact and SST1473 never claims it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecimalSelfComparisonIsReportedAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Same(decimal price) => {|SST1474:price == price|};
            }
            """);

    /// <summary>Verifies a self-comparison of a constrained type parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericSelfComparisonIsReportedAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C<T>
                where T : class
            {
                public bool Same(T value) => {|SST1474:value == value|};
            }
            """);

    /// <summary>Verifies a static member read is compared structurally like any other name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticMemberSelfComparisonIsReportedAsync()
        => await VerifyIdenticalOperands.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool Same() => {|SST1474:int.MaxValue == int.MaxValue|};

                public bool Different() => int.MaxValue == int.MinValue;
            }
            """);
}
