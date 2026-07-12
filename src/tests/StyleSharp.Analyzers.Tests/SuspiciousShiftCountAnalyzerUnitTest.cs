// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyShift = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1478SuspiciousShiftCountAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1478 (shift counts should be within the operand's width).</summary>
public class SuspiciousShiftCountAnalyzerUnitTest
{
    /// <summary>Verifies a count at or beyond a 32-bit operand's width is reported and one inside it is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountAtOrBeyondTheWidthIsReportedAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Exactly(int value) => {|SST1478:value << 32|};

                public int Beyond(int value) => {|SST1478:value >> 33|};

                public int AtLimit(int value) => value << 31;

                public int Small(int value) => value >> 1;
            }
            """);

    /// <summary>Verifies a shift by a constant zero is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroCountIsReportedAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Left(int value) => {|SST1478:value << 0|};

                public int Right(int value) => {|SST1478:value >> 0|};
            }
            """);

    /// <summary>Verifies a negative count is reported, since it masks around to a large shift.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegativeCountIsReportedAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Negative(int value) => {|SST1478:value << -1|};
            }
            """);

    /// <summary>Verifies a 64-bit operand is measured against 64 bits, not 32.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SixtyFourBitOperandUsesTheWiderLimitAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public long InRange(long value) => value << 63;

                public long AtWidth(long value) => {|SST1478:value << 64|};

                public ulong UnsignedAtWidth(ulong value) => {|SST1478:value >> 64|};

                public ulong UnsignedInRange(ulong value) => value >> 32;
            }
            """);

    /// <summary>Verifies an operand narrower than an int is measured at the width it is promoted to.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// This is the case the rule most easily gets wrong. A <c>byte</c> shifted by 8 is not masked away and
    /// is not a bug: the built-in shift operators only exist for <c>int</c>, <c>uint</c>, <c>long</c> and
    /// <c>ulong</c>, so the byte is promoted to an <c>int</c> before it is shifted, and a count of 8 is well
    /// inside 32. Only a count of 32 or more is out of range for it.
    /// </remarks>
    [Test]
    public async Task NarrowOperandsArePromotedToIntAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int ByteByItsOwnWidth(byte value) => value << 8;

                public int ByteNearTheIntWidth(byte value) => value << 31;

                public int ShortByItsOwnWidth(short value) => value << 16;

                public int UnsignedShort(ushort value) => value << 20;

                public int Character(char value) => value << 24;

                public int ByteBeyondTheIntWidth(byte value) => {|SST1478:value << 32|};

                public int ShortBeyondTheIntWidth(short value) => {|SST1478:value << 40|};
            }
            """);

    /// <summary>Verifies an unsigned 32-bit operand is measured against 32 bits.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedIntOperandUsesTheNarrowLimitAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public uint Beyond(uint value) => {|SST1478:value >> 40|};

                public uint InRange(uint value) => value >> 16;
            }
            """);

    /// <summary>Verifies the unsigned right shift is measured like the other two.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedRightShiftIsMeasuredAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Beyond(int value) => {|SST1478:value >>> 32|};

                public int Zero(int value) => {|SST1478:value >>> 0|};

                public int InRange(int value) => value >>> 8;
            }
            """);

    /// <summary>Verifies a count that is constant without being a literal is still measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantCountsThatAreNotLiteralsAreMeasuredAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private const int Bits = 32;

                public int Named(int value) => {|SST1478:value << Bits|};

                public int Folded(int value) => {|SST1478:value << (16 + 16)|};

                public int Hexadecimal(int value) => {|SST1478:value << 0x20|};

                public int Separated(int value) => value << 1_6;
            }
            """);

    /// <summary>Verifies a count the compiler cannot fold is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputedCountIsCleanAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Computed(int value, int count) => value << count;

                public int Derived(int value, int count) => value << (count + 1);
            }
            """);

    /// <summary>Verifies a native integer is never reported, because its width depends on the process.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NativeIntegerIsNotReportedAsync()
        => await VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public nint Signed(nint value) => value << 64;

                public nuint Unsigned(nuint value) => value >> 32;
            }
            """);

    /// <summary>Verifies a shift by zero can be allowed, without letting an out-of-range count through.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroCountCanBeAllowedAsync()
    {
        var test = new VerifyShift.Test
        {
            TestCode = """
                       public sealed class C
                       {
                           public int Table(int value) => (value << 0) | (value << 8) | (value << 16) | (value << 24);

                           public int Beyond(int value) => {|SST1478:value << 32|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1478.allow_zero_shift = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific key overrides the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificKeyWinsOverGeneralAsync()
    {
        var test = new VerifyShift.Test
        {
            TestCode = """
                       public sealed class C
                       {
                           public int Zero(int value) => {|SST1478:value << 0|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.allow_zero_shift = true
            stylesharp.SST1478.allow_zero_shift = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide key applies when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralKeyAppliesAsync()
    {
        var test = new VerifyShift.Test
        {
            TestCode = """
                       public sealed class C
                       {
                           public int Zero(int value) => value << 0;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.allow_zero_shift = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable value keeps the default rather than turning half the rule off.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnparsableValueFallsBackToTheDefaultAsync()
    {
        var test = new VerifyShift.Test
        {
            TestCode = """
                       public sealed class C
                       {
                           public int Zero(int value) => {|SST1478:value << 0|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1478.allow_zero_shift = sometimes

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
