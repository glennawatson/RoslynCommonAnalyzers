// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyShift = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1478SuspiciousShiftCountAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1478 (shift counts should be within the operand's width).</summary>
public class SuspiciousShiftCountAnalyzerUnitTest
{
    /// <summary>The path the analyzer config file is added at in the test workspace.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>Checks implicitly convertible count types retain their constant values.</summary>
    /// <param name="count">The typed constant count.</param>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("(short)32")]
    [Arguments("(ushort)32")]
    [Arguments("(byte)32")]
    [Arguments("(sbyte)32")]
    [Arguments("' '")]
    [Arguments("0x20")]
    [Arguments("3_2")]
    [Arguments("032")]
    public Task TypedConstantAtWidthIsReportedAsync(string count) =>
        VerifyShift.VerifyAnalyzerAsync($$"""class C { int M(int value) => {|SST1478:value << {{count}}|}; }""");

    /// <summary>Checks suffixed counts and operators accepting non-integer constants are ignored.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsupportedCountTypesAreIgnoredAsync() =>
        VerifyShift.VerifyAnalyzerAsync("""
            class C
            {
                public static C operator <<(C value, double count) => value;
                public static C operator >>(C value, long count) => value;
                C M(C value) => value << 0d;
                C N(C value) => value >> 0L;
                C Fraction(C value) => value << .5;
            }
            """);

    /// <summary>Checks lifted shifts use the underlying integer width.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullableOperandsKeepUnderlyingWidthAsync() =>
        VerifyShift.VerifyAnalyzerAsync("""
            class C
            {
                int? M(int? value) => {|SST1478:value << 32|};
                long? N(long? value) => {|SST1478:value >> 64|};
                long? Safe(long? value) => value << 32;
            }
            """);

    /// <summary>Checks a zero shift in an assembly attribute is reported without a containing member.</summary>
    /// <returns>The verification task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AssemblyAttributeZeroShiftIsReportedAsync() =>
        VerifyShift.VerifyAnalyzerAsync("""
            [assembly: Count({|SST1478:1 << 0|})]
            class CountAttribute : System.Attribute
            {
                public CountAttribute(int count) { }
            }
            """);

    /// <summary>Checks an untyped invalid left operand is ignored without a width to measure.</summary>
    /// <returns>The verification task.</returns>
    [Test]
    public async Task UntypedOperandIsIgnoredAsync()
    {
        var test = new VerifyShift.Test { TestCode = "class C { object M() => null << 32; }", CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a count at or beyond a 32-bit operand's width is reported and one inside it is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CountAtOrBeyondTheWidthIsReportedAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Exactly(int value) => {|SST1478:value << 32|};

                public int Beyond(int value) => {|SST1478:value >> 33|};

                public int AtLimit(int value) => value << 31;

                public int Small(int value) => value >> 1;
            }
            """);

    /// <summary>Verifies a zero shift that spells out a flag's bit position is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>1 &lt;&lt; 0</c> names the first bit rather than computing anything, and it is what a consistent
    /// shift style writes there — so the enum member form needs no setting to stay quiet. A count that is out
    /// of range is still a defect in an enum, and is still reported.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ZeroCountInAnEnumMemberIsCleanAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum Access
            {
                None = 0,
                Read = 1 << 0,
                Write = 1 << 1,
                Execute = 1 << 2,
            }

            public enum Broken
            {
                Overflowed = {|SST1478:1 << 32|},
            }

            public sealed class C
            {
                private readonly int _packed = {|SST1478:1 << 0|};

                public int Pack(int value)
                {
                    return {|SST1478:value << 0|};
                }
            }
            """);

    /// <summary>Verifies a shift by a constant zero is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ZeroCountIsReportedAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Left(int value) => {|SST1478:value << 0|};

                public int Right(int value) => {|SST1478:value >> 0|};
            }
            """);

    /// <summary>Verifies a negative count is reported, since it masks around to a large shift.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NegativeCountIsReportedAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Negative(int value) => {|SST1478:value << -1|};
            }
            """);

    /// <summary>Verifies a 64-bit operand is measured against 64 bits, not 32.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SixtyFourBitOperandUsesTheWiderLimitAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NarrowOperandsArePromotedToIntAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsignedIntOperandUsesTheNarrowLimitAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public uint Beyond(uint value) => {|SST1478:value >> 40|};

                public uint InRange(uint value) => value >> 16;
            }
            """);

    /// <summary>Verifies the unsigned right shift is measured like the other two.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsignedRightShiftIsMeasuredAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstantCountsThatAreNotLiteralsAreMeasuredAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ComputedCountIsCleanAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Computed(int value, int count) => value << count;

                public int Derived(int value, int count) => value << (count + 1);
            }
            """);

    /// <summary>Verifies a native integer is never reported, because its width depends on the process.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NativeIntegerIsNotReportedAsync() =>
        VerifyShift.VerifyAnalyzerAsync(
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
            (EditorConfigPath, """
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
            (EditorConfigPath, """
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
            (EditorConfigPath, """
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
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1478.allow_zero_shift = sometimes

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
