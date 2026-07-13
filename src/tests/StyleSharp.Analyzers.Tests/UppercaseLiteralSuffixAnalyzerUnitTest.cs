// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySuffix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2244UppercaseLiteralSuffixAnalyzer,
    StyleSharp.Analyzers.Sst2244UppercaseLiteralSuffixCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2244UppercaseLiteralSuffixAnalyzer"/> and its code fix (SST2244).</summary>
public class UppercaseLiteralSuffixAnalyzerUnitTest
{
    /// <summary>Verifies a lower-case long suffix — the one that reads as a digit — is reported and upper-cased.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public long M() => 1{|SST2244:l|};", "public long M() => 1L;");

    /// <summary>Verifies a lower-case unsigned suffix is reported and upper-cased.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public uint M() => 1{|SST2244:u|};", "public uint M() => 1U;");

    /// <summary>Verifies a lower-case float suffix is reported and upper-cased.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FloatSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public float M() => 1.5{|SST2244:f|};", "public float M() => 1.5F;");

    /// <summary>Verifies a lower-case double suffix is reported and upper-cased.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DoubleSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public double M() => 1{|SST2244:d|};", "public double M() => 1D;");

    /// <summary>Verifies a lower-case decimal suffix is reported and upper-cased.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecimalSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public decimal M() => 1{|SST2244:m|};", "public decimal M() => 1M;");

    /// <summary>Verifies a two-character unsigned-long suffix is reported and upper-cased as a whole.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsignedLongSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public ulong M() => 1{|SST2244:ul|};", "public ulong M() => 1UL;");

    /// <summary>Verifies the reversed two-character suffix is reported and upper-cased as a whole.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongUnsignedSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public ulong M() => 1{|SST2244:lu|};", "public ulong M() => 1LU;");

    /// <summary>Verifies a suffix that is only half lower case is reported and fully upper-cased.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedCaseSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public ulong M() => 1{|SST2244:Lu|};", "public ulong M() => 1LU;");

    /// <summary>Verifies a hex literal keeps its lower-case digits; only the suffix changes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HexLiteralKeepsItsDigitsAsync()
        => await VerifyMemberAsync("public long M() => 0xff{|SST2244:l|};", "public long M() => 0xffL;");

    /// <summary>Verifies a binary literal's suffix is reported and upper-cased.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BinaryLiteralSuffixIsFlaggedAndFixedAsync()
        => await VerifyMemberAsync("public uint M() => 0b1010{|SST2244:u|};", "public uint M() => 0b1010U;");

    /// <summary>Verifies digit separators are carried through untouched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DigitSeparatorsSurviveTheFixAsync()
        => await VerifyMemberAsync("public long M() => 1_000{|SST2244:l|};", "public long M() => 1_000L;");

    /// <summary>Verifies an exponent is not mistaken for part of the suffix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExponentIsNotPartOfTheSuffixAsync()
        => await VerifyMemberAsync("public float M() => 1.5e3{|SST2244:f|};", "public float M() => 1.5e3F;");

    /// <summary>Verifies an already upper-case suffix stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UpperCaseSuffixIsCleanAsync()
        => await VerifyMemberAsync("public ulong M() => 1UL;");

    /// <summary>Verifies a literal with no suffix at all stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralWithoutSuffixIsCleanAsync()
        => await VerifyMemberAsync("public double M() => 1.5e3;");

    /// <summary>Verifies a hex literal's trailing 'f' digits are digits, not a float suffix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HexDigitsAreNotASuffixAsync()
        => await VerifyMemberAsync("public int M() => 0xff;");

    /// <summary>Verifies a hex literal whose suffix is already upper case keeps its lower-case digits and stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HexWithUpperCaseSuffixIsCleanAsync()
        => await VerifyMemberAsync("public long M() => 0xffL;");

    /// <summary>Verifies a plain integer literal stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainIntegerIsCleanAsync()
        => await VerifyMemberAsync("public int M() => 42;");

    /// <summary>Runs a verification for one class member.</summary>
    /// <param name="member">The member source line, with markup.</param>
    /// <param name="fixedMember">The expected member source line after the fix, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyMemberAsync(string member, string? fixedMember = null)
    {
        var source = WrapMember(member);
        if (fixedMember is null)
        {
            await VerifySuffix.VerifyAnalyzerAsync(source);
            return;
        }

        await VerifySuffix.VerifyCodeFixAsync(source, WrapMember(fixedMember));
    }

    /// <summary>Wraps one member in a compilable class.</summary>
    /// <param name="member">The member source line.</param>
    /// <returns>The wrapped source.</returns>
    private static string WrapMember(string member)
        => $$"""
            internal class C
            {
                {{member}}
            }
            """;
}
