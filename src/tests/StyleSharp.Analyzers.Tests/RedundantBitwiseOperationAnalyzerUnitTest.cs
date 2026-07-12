// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBitwise = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1481RedundantBitwiseOperationAnalyzer,
    StyleSharp.Analyzers.Sst1481RedundantBitwiseOperationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1481 (bitwise operations should not use identity operands) and its fix.</summary>
public class RedundantBitwiseOperationAnalyzerUnitTest
{
    /// <summary>Verifies an identity operand on the right is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityOperandsOnTheRightAreRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Or(int value) => {|SST1481:value | 0|};

                                  public int Xor(int value) => {|SST1481:value ^ 0|};

                                  public int AndAllBits(int value) => {|SST1481:value & ~0|};

                                  public int AndMinusOne(int value) => {|SST1481:value & -1|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Or(int value) => value;

                                       public int Xor(int value) => value;

                                       public int AndAllBits(int value) => value;

                                       public int AndMinusOne(int value) => value;
                                   }
                                   """;
        await VerifyBitwise.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an identity operand on the left is reported and the right operand survives.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityOperandOnTheLeftIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Or(int value) => {|SST1481:0 | value|};

                                  public int Xor(int value) => {|SST1481:0 ^ value|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Or(int value) => value;

                                       public int Xor(int value) => value;
                                   }
                                   """;
        await VerifyBitwise.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies all-bits-set is decided against the width of the operation, not of the literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A 64-bit operation keeps every bit of a sign-extended <c>-1</c>, and <c>ulong.MaxValue</c> is the
    /// same constant written the other way round.
    /// </remarks>
    [Test]
    public async Task WideOperationsUseTheirOwnAllBitsSetAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public long Signed(long value) => {|SST1481:value & -1|};

                                  public ulong Unsigned(ulong value) => {|SST1481:value & ulong.MaxValue|};

                                  public long Zero(long value) => {|SST1481:0 | value|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public long Signed(long value) => value;

                                       public ulong Unsigned(ulong value) => value;

                                       public long Zero(long value) => value;
                                   }
                                   """;
        await VerifyBitwise.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a named constant is read like a literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedConstantOperandIsRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private const int None = 0;

                                  public int Or(int value) => {|SST1481:value | None|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private const int None = 0;

                                       public int Or(int value) => value;
                                   }
                                   """;
        await VerifyBitwise.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mask of zero is reported, and deliberately carries no fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The result is always zero, which is almost never what the author wanted. Rewriting it to <c>0</c>
    /// would preserve the behaviour and hide the bug, so the rule reports and stops.
    /// </remarks>
    [Test]
    public async Task MaskOfZeroIsReportedWithoutAFixAsync()
        => await VerifyBitwise.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Right(int value) => {|SST1481:value & 0|};

                public int Left(int value) => {|SST1481:0 & value|};
            }
            """);

    /// <summary>Verifies a zero shift is left entirely to SST1478.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A zero shift is the same class of mistake, but SST1478 knows the operand's width and can say what
    /// the shift actually does. Reporting it here as well would say the same thing twice.
    /// </remarks>
    [Test]
    public async Task ZeroShiftIsNotReportedHereAsync()
        => await VerifyBitwise.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Left(int value) => value << 0;

                public int Right(int value) => value >> 0;

                public int Unsigned(int value) => value >>> 0;
            }
            """);

    /// <summary>Verifies boolean operands are left to SST1468.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BooleanOperandsAreNotReportedAsync()
        => await VerifyBitwise.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool Or(bool value) => value | false;

                public bool And(bool value) => value & true;

                public bool Xor(bool left, bool right) => left ^ right;
            }
            """);

    /// <summary>Verifies an enum operation is not reported, since it has no integral operation type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumOperandsAreNotReportedAsync()
        => await VerifyBitwise.VerifyAnalyzerAsync(
            """
            [System.Flags]
            public enum Access
            {
                None = 0,
                Read = 1,
            }

            public sealed class C
            {
                public Access Or(Access value) => value | Access.None;

                public Access And(Access value) => value & Access.None;
            }
            """);

    /// <summary>Verifies a mask that is not an identity for the operation's width is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A <c>byte</c> is promoted to an <c>int</c> before it is masked, so all-bits-set for
    /// <c>value &amp; 0xFF</c> is <c>-1</c>, not <c>0xFF</c> — the defensive narrowing everybody writes is
    /// left alone.
    /// </remarks>
    [Test]
    public async Task NonIdentityConstantIsCleanAsync()
        => await VerifyBitwise.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int NarrowingMask(byte value) => value & 0xFF;

                public int Mask(int value) => value & 0x0F;

                public int SetBit(int value) => value | 1;

                public int Flip(int value) => value ^ -1;
            }
            """);

    /// <summary>Verifies an operation with no constant operand is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantOperandsAreCleanAsync()
        => await VerifyBitwise.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Combine(int left, int right) => left | right;

                public int Masked(int value, int[] masks) => value & masks[0];

                public int Computed(int value) => value | Next();

                private static int Next() => 0;
            }
            """);

    /// <summary>Verifies parentheses are dropped when what survives cannot be regrouped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesesAroundAPrimaryOperandAreDroppedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Nested(int left, int right) => left & ({|SST1481:right | 0|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Nested(int left, int right) => left & right;
                                   }
                                   """;
        await VerifyBitwise.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies parentheses are kept when dropping them would regroup the surviving operands.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks><c>a &amp; b ^ c</c> is <c>(a &amp; b) ^ c</c>, which is not what the source said.</remarks>
    [Test]
    public async Task ParenthesesAroundACompoundOperandAreKeptAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Nested(int a, int b, int c) => a & ({|SST1481:b ^ c | 0|});
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Nested(int a, int b, int c) => a & (b ^ c);
                                   }
                                   """;
        await VerifyBitwise.VerifyCodeFixAsync(Source, FixedSource);
    }
}
