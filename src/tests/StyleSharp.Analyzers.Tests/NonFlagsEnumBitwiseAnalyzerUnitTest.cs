// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNonFlagsEnumBitwise = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2458NonFlagsEnumBitwiseAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2458 (bitwise operations on an enum that is not a flag set).</summary>
public class NonFlagsEnumBitwiseAnalyzerUnitTest
{
    /// <summary>Verifies or-ing two members of a non-flags enum is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrOnNonFlagsEnumIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Combine() => {|SST2458:Color.Red | Color.Blue|};
            }
            """);

    /// <summary>Verifies masking a non-flags enum value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AndOnNonFlagsEnumIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Mask(Color value) => {|SST2458:value & Color.Blue|};
            }
            """);

    /// <summary>Verifies xor-ing two non-flags enum values is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XorOnNonFlagsEnumIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Toggle(Color value) => {|SST2458:value ^ Color.Green|};
            }
            """);

    /// <summary>Verifies complementing a non-flags enum value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComplementOnNonFlagsEnumIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Invert(Color value) => {|SST2458:~value|};
            }
            """);

    /// <summary>Verifies an or-assignment on a non-flags enum is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrAssignmentOnNonFlagsEnumIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Add(Color value)
                {
                    {|SST2458:value |= Color.Blue|};
                    return value;
                }
            }
            """);

    /// <summary>Verifies an and-assignment on a non-flags enum is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AndAssignmentOnNonFlagsEnumIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Keep(Color value)
                {
                    {|SST2458:value &= Color.Blue|};
                    return value;
                }
            }
            """);

    /// <summary>Verifies a xor-assignment on a non-flags enum is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XorAssignmentOnNonFlagsEnumIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Flip(Color value)
                {
                    {|SST2458:value ^= Color.Green|};
                    return value;
                }
            }
            """);

    /// <summary>Verifies a chain of bitwise operators is reported once, at the outermost operation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedOperatorsReportOnceAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color Combine() => {|SST2458:Color.Red | Color.Green | Color.Blue|};

                public static Color Strip(Color value) => {|SST2458:value & ~Color.Blue|};
            }
            """);

    /// <summary>Verifies a masked value compared to a named member is still reported: without flags, the mask lies.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaskedComparisonToMemberIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static bool HasBlue(Color value) => ({|SST2458:value & Color.Blue|}) == Color.Blue;
            }
            """);

    /// <summary>Verifies a masked zero test is still reported: the bits it examines were never assigned.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroTestMaskIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static bool Any(Color value) => ({|SST2458:value & Color.Blue|}) != 0;
            }
            """);

    /// <summary>Verifies the lifted operators on a nullable non-flags enum are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableEnumOperandIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            #nullable enable

            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static Color? Combine(Color? value) => {|SST2458:value | Color.Blue|};
            }
            """);

    /// <summary>Verifies a metadata enum without flags is reported just like a source one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MetadataEnumWithoutFlagsIsReportedAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            using System;

            public static class C
            {
                public static DayOfWeek Combine(DayOfWeek day) => {|SST2458:day | DayOfWeek.Monday|};
            }
            """);

    /// <summary>Verifies every bitwise shape is clean on an enum declared as a flag set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FlagsEnumBitwiseIsCleanAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Options
            {
                None = 0,
                Cache = 1,
                Retry = 2,
                Log = 4,
            }

            public static class C
            {
                public static Options Combine(Options value)
                {
                    var combined = value | Options.Cache;
                    var masked = combined & Options.Retry;
                    var inverted = ~Options.Log;
                    masked |= Options.Log;
                    masked &= ~Options.Cache;
                    masked ^= Options.Retry;
                    return masked & inverted;
                }
            }
            """);

    /// <summary>Verifies equality comparisons are the supported way to use a non-flags enum, and are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityComparisonsAreCleanAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static bool IsRed(Color value) => value == Color.Red;

                public static bool IsNotBlue(Color value) => value != Color.Blue;
            }
            """);

    /// <summary>Verifies enum values cast to a numeric type are raw numbers, and combining them is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NumericCastOperandsAreCleanAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green,
                Blue,
            }

            public static class C
            {
                public static int Pack(Color first, Color second) => (int)first | ((int)second << 8);

                public static int Hash(Color value) => (int)value ^ 397;
            }
            """);

    /// <summary>Verifies bitwise work on integers never binds the rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntegerBitwiseIsCleanAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static int Mask(int value)
                {
                    var low = value & 0xFF;
                    low |= 0x100;
                    return low ^ ~value;
                }
            }
            """);

    /// <summary>Verifies the non-short-circuiting boolean operators are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BooleanOperatorsAreCleanAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static bool Both(bool left, bool right) => left & right;

                public static bool Either(bool left, bool right) => left | right;
            }
            """);

    /// <summary>Verifies a metadata enum declared as a flag set is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MetadataFlagsEnumIsCleanAsync()
        => await VerifyNonFlagsEnumBitwise.VerifyAnalyzerAsync(
            """
            using System;

            public static class C
            {
                public static AttributeTargets Combine() => AttributeTargets.Class | AttributeTargets.Struct;
            }
            """);
}
