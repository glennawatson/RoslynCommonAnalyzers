// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyComparison = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2489TypeDecidedComparisonAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2489 (a relational comparison the operand's integer type already decides).</summary>
public class TypeDecidedComparisonAnalyzerUnitTest
{
    /// <summary>Verifies an unsigned <c>&gt;= 0</c> is reported as always true across every unsigned type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedAtLeastZeroIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool U(uint u) => {|SST2489:u >= 0|};
                public bool L(ulong u) => {|SST2489:u >= 0|};
                public bool S(ushort u) => {|SST2489:u >= 0|};
                public bool B(byte u) => {|SST2489:u >= 0|};
                public bool N(nuint u) => {|SST2489:u >= 0|};
            }
            """);

    /// <summary>Verifies an unsigned <c>&lt; 0</c> is reported as always false.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedBelowZeroIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(uint u) => {|SST2489:u < 0|};
                public bool N(nuint u) => {|SST2489:u < 0|};
            }
            """);

    /// <summary>Verifies an unsigned <c>&gt; 0</c> is reported as an inequality in disguise.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedAboveZeroIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(uint u) => {|SST2489:u > 0|};
            }
            """);

    /// <summary>Verifies a value compared to its type's maximum is reported as always true.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AtMostMaximumIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool B(byte b) => {|SST2489:b <= 255|};
                public bool S(ushort s) => {|SST2489:s <= 65535|};
                public bool U(uint u) => {|SST2489:u <= uint.MaxValue|};
                public bool L(ulong u) => {|SST2489:u <= ulong.MaxValue|};
                public bool H(short s) => {|SST2489:s <= short.MaxValue|};
                public bool G(long l) => {|SST2489:l <= long.MaxValue|};
            }
            """);

    /// <summary>Verifies a value compared above its type's maximum is reported as always false.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AboveMaximumIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(byte b) => {|SST2489:b > 255|};
            }
            """);

    /// <summary>Verifies a signed value compared to its type's minimum is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SignedAtMinimumIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool I(int i) => {|SST2489:i >= int.MinValue|};
                public bool B(sbyte s) => {|SST2489:s >= -128|};
                public bool H(short s) => {|SST2489:s >= short.MinValue|};
            }
            """);

    /// <summary>Verifies a signed value below its type's minimum is reported as always false.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SignedBelowMinimumIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int i) => {|SST2489:i < int.MinValue|};
            }
            """);

    /// <summary>Verifies a signed strict comparison to the minimum is reported as an inequality in disguise.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SignedAboveMinimumIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int i) => {|SST2489:i > int.MinValue|};
            }
            """);

    /// <summary>Verifies the bound may sit on either side of the comparison.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoundOnTheLeftIsReportedAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool AtLeast(uint u) => {|SST2489:0 <= u|};
                public bool Above(uint u) => {|SST2489:0 < u|};
                public bool Below(uint u) => {|SST2489:0 > u|};
                public bool AtMost(byte b) => {|SST2489:255 >= b|};
            }
            """);

    /// <summary>Verifies a signed value compared to zero is a real test and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SignedComparedToZeroIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool AtLeast(int i) => i >= 0;
                public bool Below(int i) => i < 0;
            }
            """);

    /// <summary>Verifies an interior bound asks a real question and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InteriorBoundIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool Above(uint u) => u > 5;
                public bool AtMost(byte b) => b <= 100;
            }
            """);

    /// <summary>Verifies an unsigned <c>&lt;= 0</c>, which the rule leaves to the equality it really is, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedAtMostZeroIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(uint u) => u <= 0;
            }
            """);

    /// <summary>Verifies a comparison at the maximum that only tests equality, not a range, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DegenerateAtMaximumIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool AtLeast(byte b) => b >= 255;
                public bool Below(byte b) => b < 255;
            }
            """);

    /// <summary>Verifies a native unsigned value against a non-minimum bound is not reported, since its maximum is platform-dependent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NativeUnsignedNonMinimumBoundIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool AtMost(nuint n) => n <= 5;
                public bool Above(nuint n) => n > 5;
            }
            """);

    /// <summary>Verifies two variables with no constant bound are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoVariablesAreCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int a, int b) => a < b;
            }
            """);

    /// <summary>Verifies a comparison with a bound on both sides is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoConstantsAreCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M() => 0 < 1;
            }
            """);

    /// <summary>Verifies a member operand that is not a min/max bound is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberOperandIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Offset { get; }

                public bool M(C c) => c.Offset > 0;
            }
            """);

    /// <summary>Verifies a non-integer operand is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonIntegerOperandIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(char c) => c >= 0;
            }
            """);

    /// <summary>Verifies a non-integral constant bound is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonIntegralConstantIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(double d) => d > 0.0;
            }
            """);

    /// <summary>Verifies a <c>MaxValue</c> member that is not a constant is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantMaxValueMemberIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class Holder
            {
                public uint MaxValue { get; }
            }

            public sealed class C
            {
                public bool M(uint n, Holder h) => n >= h.MaxValue;
            }
            """);

    /// <summary>Verifies a constant operand is not reported, since the compiler already folds it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantOperandIsCleanAsync()
        => await VerifyComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M()
                {
                    const uint z = 0;
                    return z >= 0;
                }
            }
            """);
}
